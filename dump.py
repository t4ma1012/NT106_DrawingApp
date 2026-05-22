import json

log_path = r"C:\Users\Admin\.gemini\antigravity\brain\86e92831-531b-48c9-b1f8-d4f363f67e8a\.system_generated\logs\transcript.jsonl"
with open(log_path, 'r', encoding='utf-8') as f:
    for line in f:
        if 'private void BtnExport_Click' in line or 'NetworkEvents_OnSyncBoardReceived' in line:
            # We found the line. Let's dump this JSON line to a file so we can inspect it.
            with open("dump.json", "w", encoding="utf-8") as out:
                out.write(line)
            print("Dumped line to dump.json")
            break
