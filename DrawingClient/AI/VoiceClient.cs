// ============================================================
// DrawingClient/AI/VoiceClient.cs
// Tuần 5 — Voice-to-Draw: nhận dạng lệnh qua giọng nói
// Dùng System.Speech.Recognition (.NET Framework built-in)
// Offline hoàn toàn, không cần API key, không cần internet
// ============================================================
using System;
using System.Collections.Generic;
using System.Speech.Recognition;
using System.Threading.Tasks;
using DrawingClient.Network;

namespace DrawingClient.AI
{
    /// <summary>
    /// Voice-to-Draw client: nhận lệnh giọng nói, thực thi tác vụ UI local.
    /// Voice lệnh KHÔNG gửi qua mạng (chỉ tác động local).
    /// </summary>
    public class VoiceClient
    {
        private SpeechRecognitionEngine _recognizer;
        private bool _isListening = false;

        // ── EVENT: thông báo lệnh được nhận dạng ─────────────────
        public event Action<string> OnCommandRecognized;  // lệnh được nhận dạng (ví dụ "circle")
        public event Action<string> OnError;              // lỗi nhận dạng

        /// <summary>
        /// Khởi tạo Voice recognizer.
        /// Chạy một lần khi ứng dụng khởi động.
        /// </summary>
        public void Initialize()
        {
            try
            {
                _recognizer = new SpeechRecognitionEngine();
                _recognizer.LoadGrammar(BuildVoiceGrammar());
                _recognizer.SpeechRecognized += (s, e) =>
                {
                    string command = e.Result.Text.ToLower().Trim();
                    Console.WriteLine($"[VoiceClient] Nhận dạng: '{command}' (confidence: {e.Result.Confidence:P})");
                    OnCommandRecognized?.Invoke(command);
                };
                _recognizer.SpeechRecognitionRejected += (s, e) =>
                {
                    Console.WriteLine($"[VoiceClient] Từ chối: {e.Result.Text}");
                };
                _recognizer.RecognizeAsyncStop();
                Console.WriteLine("[VoiceClient] Voice recognition initialized.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VoiceClient] Khởi tạo lỗi: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Bắt đầu nghe lệnh giọng nói.
        /// Thường gọi khi người dùng giữ phím V, hoặc bấm nút "Start Voice".
        /// </summary>
        public void StartListening()
        {
            if (_recognizer == null)
            {
                OnError?.Invoke("Voice recognizer not initialized.");
                return;
            }

            if (_isListening) return;

            try
            {
                _isListening = true;
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                Console.WriteLine("[VoiceClient] Bắt đầu nghe...");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                _isListening = false;
            }
        }

        /// <summary>
        /// Dừng nghe lệnh giọng nói.
        /// Gọi khi người dùng nhả phím V.
        /// </summary>
        public void StopListening()
        {
            if (!_isListening) return;

            try
            {
                _isListening = false;
                _recognizer.RecognizeAsyncStop();
                Console.WriteLine("[VoiceClient] Dừng nghe.");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
            }
        }

        public void Dispose()
        {
            _recognizer?.Dispose();
        }

        // ── GRAMMAR DEFINITION ──────────────────────────────────

        private Grammar BuildVoiceGrammar()
        {
            var choices = new Choices();

            // Tool commands
            choices.Add(new string[] { "pen", "cây bút", "vẽ" });               // Select Pen tool
            choices.Add(new string[] { "line", "đường thẳng", "kẻ" });          // Select Line tool
            choices.Add(new string[] { "rectangle", "hình chữ nhật", "vuông" }); // Select Rectangle
            choices.Add(new string[] { "circle", "tròn", "hình tròn" });         // Select Circle
            choices.Add(new string[] { "eraser", "xóa", "cục tẩy" });            // Select Eraser
            choices.Add(new string[] { "fill", "tô", "tô màu", "bucket" });     // Select Fill tool
            choices.Add(new string[] { "text", "chữ", "viết chữ" });            // Select Text tool

            // Color commands
            choices.Add(new string[] { "red", "đỏ", "màu đỏ" });                // Set color Red
            choices.Add(new string[] { "blue", "xanh", "xanh dương", "màu xanh" }); // Blue
            choices.Add(new string[] { "green", "xanh lá", "xanh lá cây" });    // Green
            choices.Add(new string[] { "yellow", "vàng", "màu vàng" });         // Yellow
            choices.Add(new string[] { "black", "đen", "màu đen" });            // Black
            choices.Add(new string[] { "white", "trắng", "màu trắng" });        // White
            choices.Add(new string[] { "purple", "tím", "màu tím" });           // Purple
            choices.Add(new string[] { "orange", "cam", "màu cam" });           // Orange

            // Action commands
            choices.Add(new string[] { "clear", "xóa tất cả", "làm sạch" });    // Clear canvas
            choices.Add(new string[] { "undo", "hoàn tác", "lùi lại" });        // Undo
            choices.Add(new string[] { "redo", "làm lại", "tiến lại" });        // Redo

            // Thickness commands
            choices.Add(new string[] { "thin", "mỏng", "dày 1" });              // Thin line
            choices.Add(new string[] { "medium", "vừa", "dày 5" });             // Medium
            choices.Add(new string[] { "thick", "dày", "dày 10" });             // Thick line

            var gb = new GrammarBuilder();
            gb.Append(choices);
            return new Grammar(gb);
        }

        // ── COMMAND EXECUTION HELPERS ───────────────────────────
        // (Gọi từ MainForm khi OnCommandRecognized được raise)

        /// <summary>
        /// Thực thi lệnh giọng nói (do Person A gọi qua event handler).
        /// Ví dụ:
        ///   voiceClient.OnCommandRecognized += (cmd) => mainForm.ExecuteVoiceCommand(cmd);
        /// </summary>
        public static CommandType GetCommandTypeFromVoice(string voiceCommand)
        {
            voiceCommand = voiceCommand.ToLower().Trim();

            // Mapping giọng nói → công cụ
            if (IsToolCommand(voiceCommand, "pen"))
                return CommandType.DRAW;   // Bên Person A xử lý
            if (IsToolCommand(voiceCommand, "circle"))
                return CommandType.DRAW;
            if (IsToolCommand(voiceCommand, "rectangle"))
                return CommandType.DRAW;
            if (IsToolCommand(voiceCommand, "line"))
                return CommandType.DRAW;
            if (IsToolCommand(voiceCommand, "eraser"))
                return CommandType.DRAW;
            if (IsToolCommand(voiceCommand, "fill"))
                return CommandType.FLOOD_FILL;
            if (IsToolCommand(voiceCommand, "text"))
                return CommandType.TEXT;

            // Action commands
            if (IsActionCommand(voiceCommand, "undo"))
                return CommandType.UNDO;
            if (IsActionCommand(voiceCommand, "redo"))
                return CommandType.REDO;
            if (IsActionCommand(voiceCommand, "clear"))
                return CommandType.CLEAR_ALL;

            return (CommandType)(-1);  // Unknown
        }

        private static bool IsToolCommand(string cmd, string tool)
        {
            return cmd.Contains(tool);
        }

        private static bool IsActionCommand(string cmd, string action)
        {
            return cmd.Contains(action);
        }

        /// <summary>
        /// Trích xuất màu từ lệnh giọng nói.
        /// Ví dụ: "đỏ" → Red, "xanh" → Blue
        /// </summary>
        public static int? GetColorFromVoiceCommand(string voiceCommand)
        {
            voiceCommand = voiceCommand.ToLower();

            if (voiceCommand.Contains("red") || voiceCommand.Contains("đỏ"))
                return System.Drawing.Color.Red.ToArgb();
            if (voiceCommand.Contains("blue") || voiceCommand.Contains("xanh dương"))
                return System.Drawing.Color.Blue.ToArgb();
            if (voiceCommand.Contains("green") || voiceCommand.Contains("xanh lá"))
                return System.Drawing.Color.Green.ToArgb();
            if (voiceCommand.Contains("yellow") || voiceCommand.Contains("vàng"))
                return System.Drawing.Color.Yellow.ToArgb();
            if (voiceCommand.Contains("black") || voiceCommand.Contains("đen"))
                return System.Drawing.Color.Black.ToArgb();
            if (voiceCommand.Contains("white") || voiceCommand.Contains("trắng"))
                return System.Drawing.Color.White.ToArgb();
            if (voiceCommand.Contains("purple") || voiceCommand.Contains("tím"))
                return System.Drawing.Color.Purple.ToArgb();
            if (voiceCommand.Contains("orange") || voiceCommand.Contains("cam"))
                return System.Drawing.Color.Orange.ToArgb();

            return null;
        }

        /// <summary>
        /// Trích xuất độ dày từ lệnh giọng nói.
        /// </summary>
        public static int? GetThicknessFromVoiceCommand(string voiceCommand)
        {
            voiceCommand = voiceCommand.ToLower();

            if (voiceCommand.Contains("thin") || voiceCommand.Contains("mỏng"))
                return 1;
            if (voiceCommand.Contains("medium") || voiceCommand.Contains("vừa"))
                return 5;
            if (voiceCommand.Contains("thick") || voiceCommand.Contains("dày"))
                return 10;

            return null;
        }
    }
}
