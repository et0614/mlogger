# Reviewing measured data

Past measurement data is managed under the **Data Files** tab.
The data is stored on the smartphone itself, so you can browse and share it without an internet connection.

## Data list

![Measured data list](assets/screenshots/data_files_list.png){ width="280" }

Data is grouped by M-Logger, with each day's file listed below it together with its file size.
If you measure more than once on the same day, the new data is appended to the file for that day.

## Sharing and deleting

Swipe a row to the left to reveal **Share** and **Delete** buttons.

![Share / Delete on swipe](assets/screenshots/data_files_swipe.png){ width="280" }

- **Share**: send the file as CSV to another app (mail / messaging / cloud storage, etc.) via the OS standard share sheet.
- **Delete**: permanently delete the data. **Deletion cannot be undone**.

The CSV file contains one sample per row with timestamp and each sensor value. It opens directly in Excel or any analysis script.

## Detail view

Tap a row to open the raw values of that measurement as a time-series table.

![Measurement detail table](assets/screenshots/data_detail_table.png){ width="280" }

Use this when you want a quick look at the values on the smartphone.
For serious analysis the assumption is that you import the CSV into a PC.

## Viewing the device operation log

Tap "View activity log" at the bottom of the data list screen to open the communication history with the M-Logger.

![Operation log](assets/screenshots/operation_log.png){ width="280" }

Operations such as time sync, measurement start, and mode change are recorded with timestamps.
Useful when you want to trace the cause of unexpected behaviour.

The buttons at the bottom let you **Share** (send as text) and **Clear log**.
