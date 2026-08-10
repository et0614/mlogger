"""
M-Logger 風速プローブ 複数風洞 校正GUI (Tkinter)

E-Sensor 版 (e-sensor/software/python/calibration_gui.py) と同じ運用モデル:
  - 風洞ごとに1行。初期状態はどの CP2112 アダプタも未割当。
  - 新しい CP2112 (USB-I2C ブリッジ) が検出されたら、未割当の風洞のどれに
    割り当てるかダイアログで選ぶ。選んだら「そのアダプタ(物理ポート) ↔ その風洞」
    は以後固定。
  - 割当済み風洞のアダプタにプローブが在れば[開始]が有効化。押すと個体確認 →
    校正 (Phase1-3) を実行し、web/velocity_calibration/reports/<6hex>.json を出力。
  - プローブ/アダプタが抜かれたら「空き」に戻る (割当=固定は保持。校正中なら中断)。
  - 各風洞は独立ワーカーで随時開始でき、他風洞の校正中でも別風洞を開始できる。

E-Sensor との相違:
  - デバイスは MIDI ではなく CP2112 経由の I2C。プローブの在/不在は
    device_id (INFO BLOCK) の読み出しで判定する。
  - プローブ在席チェックは HID を短時間 open するため、校正中の風洞は
    ポーリング対象から外す (校正ワーカーが自分で失敗を検知する)。
  - ファンは 1 台の QuadroFan の fan1..fan4 を風洞 1..4 に対応させる。

実行: python calibration_gui.py
"""
import os
import threading
import time
import tkinter as tk
from tkinter import ttk, messagebox

# 校正ワーカーは別スレッドで matplotlib を「保存」に使う。GUIバックエンドだと
# 別スレッド描画で問題が出るため、非対話の Agg を強制する(校正は show_plot=False)。
import matplotlib
matplotlib.use('Agg')

from cp2112_driver import CP2112Device
from anemometer_manager import AnemometerManager
from anemometer_calibrator import AnemometerCalibrator
from calibrate_anemometer import CALIBRATOR_PROFILES, REPORTS_DIR

POLL_INTERVAL_MS = 1500

# 抜線はこの回数だけ連続で不在を確認してから確定する(デバウンス)。ハブは1台
# 抜くと一瞬、他ポートも再列挙で消えることがあるため。
REMOVE_DEBOUNCE_MISSES = 2

# 風洞は CALIBRATOR_PROFILES に定義された ID ぶんだけ用意する。
# 風洞 id N は QuadroFan の fanN・プロファイル CALIBRATOR_PROFILES[N] に対応。
# (4風洞運用にはプロファイル 3,4 を calibrate_anemometer.py に追加すること)
TUNNEL_IDS = sorted(CALIBRATOR_PROFILES.keys())

STATUS_STYLE = {
    'free':        ('空き',           '#888888'),
    'no_probe':    ('プローブ無し',   '#888888'),
    'ready':       ('占有(準備完了)', '#1a7f37'),
    'calibrating': ('校正中',         '#0969da'),
    'done':        ('完了',           '#1a7f37'),
    'failed':      ('失敗',           '#cf222e'),
    'cancelled':   ('中断',           '#bc4c00'),
}


def probe_device_id(path):
    """アダプタ path 経由でプローブの device_id (6桁hex) を読む。不在なら None。"""
    m = AnemometerManager(path=path)
    if not m.open():
        return None
    try:
        did = m.get_device_id()
        return f"{did:06X}" if did is not None else None
    except Exception:
        return None
    finally:
        m.close()


class Tunnel:
    """1 風洞ぶんの状態とワーカー管理。"""
    def __init__(self, tunnel_id):
        self.id = tunnel_id
        self.name = f"風洞{tunnel_id}"
        self.fan_index = tunnel_id
        self.bound_path = None            # 割当済み CP2112 の HID path (固定)
        self.present_device_id = None     # 今そのアダプタに在るプローブ (無ければ None)
        self.miss_count = 0               # 連続不在カウンタ (デバウンス用)
        self.status = 'free'
        self.detail = ''
        self.progress = 0.0
        self.worker = None
        self.cancel_event = threading.Event()

    @property
    def assigned(self):
        return self.bound_path is not None

    @property
    def occupied(self):
        return self.present_device_id is not None

    @property
    def running(self):
        return self.worker is not None and self.worker.is_alive()

    @property
    def busy(self):
        """HID を占有中 (校正実行中)。ポーリングの在席チェック対象から外す。"""
        return self.running or self.status == 'calibrating'


class CalibrationGUI:
    def __init__(self, root):
        self.root = root
        root.title("M-Logger 風速プローブ 風洞校正 (複数風洞)")
        self.tunnels = {tid: Tunnel(tid) for tid in TUNNEL_IDS}
        # 割当を拒否した(ダイアログでスキップした)アダプタは、抜くまで再度聞かない。
        self._skip_paths = set()
        self._dialog_open = False
        # 取り違え防止: 実行中ワーカーが使用中の device_id 集合。
        self._claimed = set()
        self._claim_lock = threading.Lock()
        self._build_ui()
        self._stop = threading.Event()
        self._poller = threading.Thread(target=self._poll_loop, daemon=True)
        self._poller.start()
        root.protocol("WM_DELETE_WINDOW", self._on_close)

    # ---------------- UI 構築 ----------------
    def _build_ui(self):
        top = ttk.Frame(self.root, padding=8)
        top.pack(fill='both', expand=True)

        header = ttk.Frame(top)
        header.pack(fill='x')
        ttk.Label(header, text="風洞", width=8, font=('', 10, 'bold')).grid(row=0, column=0)
        ttk.Label(header, text="状態", width=16, font=('', 10, 'bold')).grid(row=0, column=1)
        ttk.Label(header, text="個体(device_id)", width=16, font=('', 10, 'bold')).grid(row=0, column=2)
        ttk.Label(header, text="進捗 / 詳細", width=44, font=('', 10, 'bold')).grid(row=0, column=3)
        ttk.Label(header, text="操作", width=16, font=('', 10, 'bold')).grid(row=0, column=4)

        self.rows = {}
        for tid in TUNNEL_IDS:
            self.rows[tid] = self._build_row(top, tid)

        bar = ttk.Frame(top, padding=(0, 8, 0, 0))
        bar.pack(fill='x')
        self.status_line = ttk.Label(bar, text="CP2112 監視中...", foreground='#57606a')
        self.status_line.pack(side='left')

    def _build_row(self, parent, tid):
        f = ttk.Frame(parent, padding=(0, 4))
        f.pack(fill='x')
        ttk.Label(f, text=self.tunnels[tid].name, width=8).grid(row=0, column=0, sticky='w')
        status = tk.Label(f, text='空き', width=16, anchor='w')
        status.grid(row=0, column=1, sticky='w')
        dev = ttk.Label(f, text='-', width=16)
        dev.grid(row=0, column=2, sticky='w')
        pf = ttk.Frame(f)
        pf.grid(row=0, column=3, sticky='w', padx=(0, 6))
        pbar = ttk.Progressbar(pf, orient='horizontal', length=260,
                               mode='determinate', maximum=100)
        pbar.pack(anchor='w')
        detail = ttk.Label(pf, text='未割当', width=42, anchor='w')
        detail.pack(anchor='w')
        start_btn = ttk.Button(f, text='校正開始', width=8,
                               command=lambda t=tid: self._on_start(t))
        start_btn.grid(row=0, column=4, sticky='w')
        start_btn.state(['disabled'])
        result_btn = ttk.Button(f, text='結果', width=6,
                                command=lambda t=tid: self._open_result(t))
        result_btn.grid(row=0, column=5, sticky='w', padx=(4, 0))
        result_btn.state(['disabled'])
        return {'status': status, 'dev': dev, 'detail': detail, 'pbar': pbar,
                'start': start_btn, 'result': result_btn}

    # ---------------- CP2112 監視ループ(別スレッド) ----------------
    def _poll_loop(self):
        while not self._stop.is_set():
            try:
                adapters = {d['path']: d for d in CP2112Device.list_devices()}
            except Exception:
                adapters = {}

            # HID open を伴うプローブ在席チェックはポーリングスレッドで行う
            # (main スレッドを固まらせない)。校正実行中の風洞はスキップ。
            probe_ids = {}
            for t in list(self.tunnels.values()):
                if t.assigned and t.bound_path in adapters and not t.busy:
                    probe_ids[t.bound_path] = probe_device_id(t.bound_path)
            # 未割当アダプタのプローブも読んでおく (割当ダイアログの表示用)
            bound = {t.bound_path for t in self.tunnels.values() if t.assigned}
            for path in adapters.keys():
                if path not in bound and path not in self._skip_paths:
                    probe_ids[path] = probe_device_id(path)

            self.root.after(0, self._apply_state, adapters, probe_ids)
            self._stop.wait(POLL_INTERVAL_MS / 1000.0)

    def _apply_state(self, adapters, probe_ids):
        """メインスレッドで監視結果を反映する。"""
        present_paths = set(adapters.keys())

        # 1) 割当済み風洞の在/不在を更新
        for t in self.tunnels.values():
            if not t.assigned:
                continue
            if t.bound_path in present_paths:
                if t.busy:
                    continue  # 校正中はワーカーに任せる
                pid = probe_ids.get(t.bound_path)
                if pid is not None:
                    t.present_device_id = pid
                    t.miss_count = 0
                    if t.status in ('free', 'no_probe'):
                        t.status, t.detail = 'ready', '準備完了。校正開始できます。'
                else:
                    # アダプタは在るがプローブが応答しない
                    t.miss_count += 1
                    if t.miss_count < REMOVE_DEBOUNCE_MISSES:
                        continue
                    t.present_device_id = None
                    if t.status not in ('no_probe',):
                        t.status, t.detail = 'no_probe', 'プローブを接続してください'
            else:
                # アダプタごと不在
                t.miss_count += 1
                if t.miss_count < REMOVE_DEBOUNCE_MISSES:
                    continue
                if t.running:
                    t.cancel_event.set()
                t.present_device_id = None
                if t.status != 'free':
                    t.status, t.detail = 'free', '(アダプタが抜かれました)'

        # 2) 未割当の新アダプタ → 割当ダイアログ
        bound_paths = {t.bound_path for t in self.tunnels.values() if t.assigned}
        self._skip_paths &= present_paths
        if not self._dialog_open:
            for path in present_paths:
                if path in bound_paths or path in self._skip_paths:
                    continue
                self._prompt_enroll(path, adapters[path], probe_ids.get(path))
                break  # 1周に1つずつ

        self._refresh_all()
        self.status_line.config(
            text=f"CP2112 監視中: 接続 {len(present_paths)} 台 / 割当済み "
                 f"{sum(1 for t in self.tunnels.values() if t.assigned)} 風洞")

    # ---------------- 割当ダイアログ ----------------
    def _prompt_enroll(self, path, adapter, probe_id):
        unassigned = [t for t in self.tunnels.values() if not t.assigned]
        if not unassigned:
            self._skip_paths.add(path)
            return

        self._dialog_open = True
        dlg = tk.Toplevel(self.root)
        dlg.title("風洞の割当")
        dlg.transient(self.root)
        dlg.grab_set()
        probe_txt = f"プローブ {probe_id}" if probe_id else "プローブ未接続"
        ttk.Label(dlg, padding=10,
                  text=f"新しい CP2112 アダプタを検出しました。\n"
                       f"  S/N: {adapter.get('serial')}  ({probe_txt})\n"
                       f"どの風洞に割り当てますか？（以後このポートは固定されます）"
                  ).pack()
        sel = tk.IntVar(value=unassigned[0].id)
        body = ttk.Frame(dlg, padding=(10, 0))
        body.pack(fill='x')
        for t in unassigned:
            ttk.Radiobutton(body, text=f"{t.name} (fan{t.fan_index})",
                            variable=sel, value=t.id).pack(anchor='w')

        btns = ttk.Frame(dlg, padding=10)
        btns.pack(fill='x')

        def do_assign():
            t = self.tunnels[sel.get()]
            t.bound_path = path
            t.present_device_id = probe_id
            if probe_id:
                t.status, t.detail = 'ready', '準備完了。校正開始できます。'
            else:
                t.status, t.detail = 'no_probe', 'プローブを接続してください'
            self._close_dialog(dlg)
            self._refresh_all()

        def do_cancel():
            self._skip_paths.add(path)
            self._close_dialog(dlg)

        ttk.Button(btns, text="割り当てる", command=do_assign).pack(side='right')
        ttk.Button(btns, text="スキップ", command=do_cancel).pack(side='right', padx=(0, 6))
        dlg.protocol("WM_DELETE_WINDOW", do_cancel)

    def _close_dialog(self, dlg):
        self._dialog_open = False
        try:
            dlg.grab_release()
            dlg.destroy()
        except Exception:
            pass

    # ---------------- 校正開始 ----------------
    def _on_start(self, tid):
        t = self.tunnels[tid]
        if t.running or not t.occupied:
            return
        t.cancel_event.clear()
        # busy 判定のため status を先に calibrating へ (ポーリングの HID open と競合させない)
        t.status, t.detail, t.progress = 'calibrating', '開始しています...', 0.0
        self._refresh_row(t)
        t.worker = threading.Thread(target=self._run_worker, args=(t,), daemon=True)
        t.worker.start()

    def _run_worker(self, t):
        device_id = t.present_device_id
        try:
            # 取り違え防止: 同一 device_id を別風洞が使用中なら中止する
            with self._claim_lock:
                if device_id in self._claimed:
                    self._ui(lambda: self._set(t, 'failed',
                             f'取り違え防止: {device_id} は別の風洞が使用中'))
                    return
                self._claimed.add(device_id)
            try:
                # ポーリングスレッドの在席チェックが HID を放すまで少し待つ
                time.sleep(0.3)

                cfg = CALIBRATOR_PROFILES[t.id]
                cal = AnemometerCalibrator(
                    cp2112_path=t.bound_path,
                    fan_index=t.fan_index,
                    calibration_points=cfg['calibration_points'],
                    validation_points=cfg['validation_points'],
                    calibrator_id=t.id,
                    expected_device_id=device_id,
                    on_progress=lambda msg, frac, tt=t: self._ui(
                        lambda: self._set(tt, 'calibrating', msg, frac)),
                    on_abnormal=lambda v, tt=t: self._ask_abnormal(tt, v),
                    should_cancel=t.cancel_event.is_set,
                )
                ok = cal.run_calibration(show_plot=False)

                if cal.wrong_device:
                    self._ui(lambda: self._set(t, 'failed', cal.error or '取り違え検出'))
                elif t.cancel_event.is_set():
                    self._ui(lambda: self._set(t, 'cancelled', '中断しました'))
                elif ok:
                    def _done(tt=t):
                        self._set(tt, 'done', '完了', 1.0)
                        self._open_result(tt.id)   # 結果PNGを自動表示
                    self._ui(_done)
                else:
                    self._ui(lambda: self._set(t, 'failed', cal.error or '校正NG'))
            finally:
                with self._claim_lock:
                    self._claimed.discard(device_id)
        except Exception as e:
            self._ui(lambda: self._set(t, 'failed', f'例外: {e}'))
        finally:
            t.worker = None
            self._ui(lambda: self._refresh_row(t))

    def _ask_abnormal(self, t, voltage):
        """0m/s 異常電圧時の続行可否をメインスレッドのダイアログで尋ねる(ブロック)。"""
        result = {'v': False}
        done = threading.Event()

        def ask():
            result['v'] = messagebox.askyesno(
                f"{t.name}: 異常電圧",
                f"0 m/s 電圧が異常に低い ({voltage*1000:.1f} mV)。\n"
                f"風速計回路の不具合が疑われます。\n\nこのまま校正を続行しますか？")
            done.set()

        self.root.after(0, ask)
        done.wait()
        return result['v']

    # ---------------- 結果表示 ----------------
    def _open_result(self, tid):
        t = self.tunnels[tid]
        dev = t.present_device_id
        if not dev:
            return
        png = os.path.join(REPORTS_DIR, f"{dev}.png")
        if os.path.exists(png):
            try:
                os.startfile(png)  # Windows 既定ビューア
            except Exception as e:
                messagebox.showerror("結果表示", f"PNG を開けません: {e}")
        else:
            messagebox.showinfo("結果表示", f"まだ結果PNGがありません:\n{png}")

    # ---------------- UI 反映ヘルパ ----------------
    def _ui(self, fn):
        self.root.after(0, fn)

    def _set(self, t, status, detail=None, progress=None):
        t.status = status
        if detail is not None:
            t.detail = detail
        if progress is not None:
            t.progress = progress
        self._refresh_row(t)

    def _refresh_all(self):
        for t in self.tunnels.values():
            self._refresh_row(t)

    def _refresh_row(self, t):
        r = self.rows[t.id]
        label, color = STATUS_STYLE.get(t.status, (t.status, '#000000'))
        r['status'].config(text=label, fg=color)
        r['dev'].config(text=t.present_device_id or '-')
        r['detail'].config(text='未割当' if not t.assigned else t.detail)
        if t.status in ('free', 'ready', 'no_probe'):
            pval = 0
        elif t.status == 'done':
            pval = 100
        else:
            pval = int(t.progress * 100)
        r['pbar']['value'] = pval
        # 開始ボタン: ready かつ非実行のときのみ有効。done/failed/cancelled では
        # 無効のまま (誤って再校正しない)。抜線→再接続で ready に戻ると再有効化。
        if t.status == 'ready' and not t.running:
            r['start'].state(['!disabled'])
        else:
            r['start'].state(['disabled'])
        if t.status == 'done':
            r['result'].state(['!disabled'])
        else:
            r['result'].state(['disabled'])

    def _on_close(self):
        self._stop.set()
        for t in self.tunnels.values():
            t.cancel_event.set()
        self.root.destroy()


def main():
    root = tk.Tk()
    CalibrationGUI(root)
    root.minsize(900, 200)
    root.mainloop()


if __name__ == '__main__':
    main()
