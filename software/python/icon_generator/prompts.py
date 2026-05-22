"""アイコン生成用プロンプトの定義。

マスタープロンプト (= 全画像の視覚契約) と、anchor / clothing / activity
それぞれのビルダー関数をここに集約する。プロンプトを修正したいときは
ここだけ触ればよい。
"""

MASTER_PROMPT = """\
Generate a 1024x1024 square illustration on a pure white background.

[Visual style]
- Flat 2D illustration, no 3D, no photorealism, no perspective vanishing.
- Outlines: dark charcoal #2E2E2E, even thickness ~6 px equivalent.
- Palette is STRICTLY GRAYSCALE + one accent:
    white #FFFFFF (background, highlights)
    light gray #E0E0E0 (fills)
    mid gray  #9E9E9E (mid tones)
    dark gray #5A5A5A (shadows / fold lines)
    charcoal  #2E2E2E (outlines)
    warm wood tan #C9A87C (mannequin body ONLY; not used elsewhere)
- No gradients beyond a single mid-tone fill. No textures. No drop shadows.
- No text, no labels, no logos, no watermark.
- Render reference: Google Material Symbols / pictogram, mildly illustrated.

[Subject anchor - identical across ALL images]
- A faceless, gender-neutral wooden artist mannequin, FULL BODY.
- Proportion: head-to-body ~1:7, smooth simplified joints, no fingers/toes.
- No face, no eyes, no mouth, no hair, no gender markers.
- Composition: centered, ~80% of canvas, ~10% padding on every side.
- View: eye level, slight 3/4 angle (~15 degrees), orthographic-ish.
- Lighting: soft even ambient, no harsh highlights.

[Hard constraints]
- Same mannequin identity in every image.
- Only the clothing or pose / props change.
- Props (when needed) rendered in the same flat grayscale style.
"""


def anchor_prompt() -> str:
    """素マネキン (T-stance lite) を生成するためのプロンプト。"""
    return (
        MASTER_PROMPT
        + "\n\n[Pose] Mannequin standing upright, arms relaxed slightly away from torso"
          " (T-stance lite), facing 3/4 toward camera. Wearing nothing."
          " This is the reference pose used by all subsequent images."
    )


_PRESERVE_BODY = (
    "CRITICAL: The mannequin's body MUST remain the warm wood tan color "
    "(#C9A87C) exactly as in the reference image. Do NOT recolor the "
    "mannequin body to gray. Grayscale palette applies ONLY to clothing, "
    "props and outlines; the mannequin body keeps its wood tan color.\n"
)


def clothing_prompt(item_description: str) -> str:
    """anchor 画像と組み合わせて使う、着衣の差分指示プロンプト。"""
    return (
        "Use the attached image as the exact style and mannequin reference.\n"
        "Keep the same mannequin, same pose (T-stance lite), same proportions,\n"
        "same line weight, same white background.\n"
        + _PRESERVE_BODY
        + f"Only change: dress the mannequin in {item_description}.\n"
        "Do not add face, hair, environment, shadow on floor, or text."
    )


def activity_prompt(activity_description: str, prop_description: str) -> str:
    """anchor 画像と組み合わせて使う、活動 (姿勢 + プロップ) の差分指示プロンプト。"""
    prop_line = (
        f"Include only the minimal prop: {prop_description}."
        if prop_description and prop_description.lower() != "none"
        else "Do not include any prop."
    )
    return (
        "Use the attached image as the exact style and mannequin reference.\n"
        "Keep the same mannequin identity, same proportions, same line weight,\n"
        "same white background.\n"
        + _PRESERVE_BODY
        + f"Only change: pose the mannequin to {activity_description}. {prop_line}\n"
        "Do not add face, hair, environment beyond the prop, shadow on floor, or text."
    )
