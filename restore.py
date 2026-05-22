import json
import re
import sys

log_path = r"C:\Users\Admin\.gemini\antigravity\brain\86e92831-531b-48c9-b1f8-d4f363f67e8a\.system_generated\logs\transcript.jsonl"
target_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\MainForm.cs"

recovered = False
with open(log_path, 'r', encoding='utf-8') as f:
    for line in f:
        try:
            data = json.loads(line)
        except:
            continue
            
        if data.get('type') == 'TOOL_RESPONSE':
            content = data.get('content', '')
            if 'MainForm.cs' in content and 'Total Lines:' in content:
                lines = content.split('\n')
                out_lines = []
                recording = False
                for l in lines:
                    if 'The following code has been modified' in l:
                        recording = True
                        continue
                    if 'The above content shows the entire, complete file contents' in l or 'The above content does NOT show the entire file contents' in l:
                        recording = False
                        
                    if recording:
                        # Match: "1: using System;"
                        # Some lines might be empty or just numbers
                        match = re.match(r'^\d+:\s(.*)', l)
                        if match:
                            out_lines.append(match.group(1))
                        else:
                            match_empty = re.match(r'^\d+:$', l.strip())
                            if match_empty:
                                out_lines.append("")
                            
                if len(out_lines) > 500:
                    with open(target_path, 'w', encoding='utf-8') as out_f:
                        out_f.write('\n'.join(out_lines))
                    print(f"Recovered {len(out_lines)} lines to {target_path}")
                    recovered = True
                    break

if not recovered:
    print("Failed to find and recover the file.")
