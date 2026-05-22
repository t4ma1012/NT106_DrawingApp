import json

log_path = r"C:\Users\Admin\.gemini\antigravity\brain\86e92831-531b-48c9-b1f8-d4f363f67e8a\.system_generated\logs\transcript.jsonl"
with open(log_path, 'r', encoding='utf-8') as f:
    for line in f:
        try:
            data = json.loads(line)
        except:
            continue
        
        # We can dump keys
        content = data.get('content', '')
        if 'private void BtnExport_Click' in content or 'NetworkEvents_OnSyncBoardReceived' in content:
            print("Found in step type:", data.get('type'))
            print("Length of content:", len(content))
            with open("found_content.txt", "w", encoding="utf-8") as out:
                out.write(content)
            break
