import json

log_path = r"C:\Users\Admin\.gemini\antigravity\brain\86e92831-531b-48c9-b1f8-d4f363f67e8a\.system_generated\logs\transcript.jsonl"
target_path = r"d:\Download\NT106_DrawingApp_Fix14\NT106_DrawingApp\DrawingClient\Forms\MainForm.cs"

found = False
with open(log_path, 'r', encoding='utf-8') as f:
    for line in f:
        try:
            data = json.loads(line)
        except Exception as e:
            continue
        
        content = data.get('content', '')
        if 'public partial class MainForm : Form' in content and 'File Path:' in content:
            # We found the response of view_file
            print("Found the file dump in log!")
            lines = content.split('\n')
            extracted_lines = []
            recording = False
            for l in lines:
                if 'The following code has been modified' in l:
                    recording = True
                    continue
                if 'The above content does NOT show' in l or 'The above content shows the entire' in l:
                    recording = False
                
                if recording:
                    # Strip "123: " prefix
                    idx = l.find(': ')
                    if idx != -1 and l[:idx].isdigit():
                        extracted_lines.append(l[idx+2:])
                    elif len(l.strip()) > 0 and l.strip()[-1] == ':' and l.strip()[:-1].isdigit():
                        extracted_lines.append("")
                        
            if len(extracted_lines) > 500:
                with open(target_path, 'w', encoding='utf-8') as out_f:
                    out_f.write('\n'.join(extracted_lines))
                print(f"Recovered {len(extracted_lines)} lines to {target_path}")
                found = True
                break

if not found:
    print("Could not find MainForm dump!")
