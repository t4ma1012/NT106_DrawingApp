using System;
using System.Drawing;
using System.Windows.Forms;
using DrawingClient.Network;
using SharedLib.Payloads;

namespace DrawingClient.Forms
{
    public class LobbyForm : Form
    {
        private readonly ClientNetwork _network;
        private readonly string _username;
        private TextBox txtRoomCode;
        private Label lblStatus;

        // ĐÂY CHÍNH LÀ HÀM NHẬN 2 THAM SỐ MÀ VISUAL STUDIO ĐANG TÌM KIẾM NÈ!
        public LobbyForm(ClientNetwork network, string username)
        {
            _network = network;
            _username = username;

            this.Text = "Sảnh chờ";
            this.Size = new Size(460, 290);
            this.StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            SubscribeEvents();
            this.FormClosed += LobbyForm_FormClosed;
            this.Resize += (s, e) => CenterContent();
        }

        private void BuildUi()
        {
            // Central flow panel to keep important bits centered
            var flp = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                Width = 380,
                Anchor = AnchorStyles.None
            };

            Label lblWelcome = new Label
            {
                Text = $"Xin chào, {_username}",
                AutoSize = true,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Margin = new Padding(3, 6, 3, 12)
            };

            Button btnCreateRoom = new Button
            {
                Text = "Tạo phòng mới",
                Size = new Size(280, 36),
                Margin = new Padding(3, 3, 3, 6)
            };
            btnCreateRoom.Click += (s, e) =>
            {
                lblStatus.Text = "Đang tạo phòng...";
                _network.SendCreateRoom();
            };

            Label lblJoin = new Label
            {
                Text = "Mã phòng:",
                AutoSize = true,
                Margin = new Padding(3, 6, 3, 0)
            };

            txtRoomCode = new TextBox
            {
                Width = 280,
                Margin = new Padding(3, 3, 3, 6)
            };

            Button btnJoinRoom = new Button
            {
                Text = "Tham gia phòng",
                Size = new Size(280, 36),
                Margin = new Padding(3, 3, 3, 6)
            };
            btnJoinRoom.Click += async (s, e) =>
            {
                string roomCode = txtRoomCode.Text?.Trim();
                if (string.IsNullOrWhiteSpace(roomCode))
                {
                    MessageBox.Show("Vui lòng nhập mã phòng.");
                    return;
                }

                lblStatus.Text = "Đang gửi yêu cầu tham gia...";

                bool routed = await _network.ReconnectToRoomOwnerViaLoadBalancerAsync(roomCode);
                if (!routed)
                {
                    lblStatus.Text = "Không thể chuyển tới server của phòng.";
                    return;
                }

                // ✅ FIX RACE CONDITION: Tạo MainForm và subscribe events TRƯỚC khi gửi JOIN_ROOM
                // Đảm bảo SYNC_BOARD đến sau khi events đã được đăng ký
                var pendingMainForm = new MainForm(_network, roomCode, false);

                // Override OnJoinRoomResponse một lần để show form nếu thành công
                Action<JoinRoomResponse> onceHandler = null;
                onceHandler = (resp) =>
                {
                    NetworkEvents.OnJoinRoomResponse -= onceHandler;
                    if (resp != null && resp.IsSuccess)
                    {
                        pendingMainForm.SetRoomOwner(resp.IsRoomOwner);
                        pendingMainForm.RegisterUdpEndpoint();
                        this.BeginInvoke(new Action(() =>
                        {
                            this.Hide();
                            pendingMainForm.FormClosed += (fs, fe) => this.Close();
                            pendingMainForm.Show();
                        }));
                    }
                    else
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            pendingMainForm.Dispose();
                            lblStatus.Text = resp?.Message ?? "Không thể vào phòng";
                        }));
                    }
                };
                NetworkEvents.OnJoinRoomResponse += onceHandler;

                _network.SendJoinRoom(roomCode);
            };

            lblStatus = new Label
            {
                AutoSize = false,
                Width = flp.Width,
                Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DimGray,
                Text = "Sẵn sàng",
                Margin = new Padding(3, 6, 3, 3)
            };

            // Logout button (will be placed on the right of the status)
            Button btnLogout = new Button
            {
                Text = "Đăng xuất",
                Size = new Size(100, 36),
                BackColor = Color.Tomato,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(6, 3, 3, 3)
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) =>
            {
                try { _network.Disconnect(); } catch { }
                var login = new LoginForm();
                this.Hide();
                login.FormClosed += (fs, fe) => this.Close();
                login.Show();
            };

            // Bottom row: status + logout button side-by-side
            var bottomRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Height = Math.Max(lblStatus.Height, btnLogout.Height) + 6,
                Width = flp.Width,
                Margin = new Padding(0, 6, 0, 0)
            };
            bottomRow.Controls.Add(lblStatus);
            bottomRow.Controls.Add(btnLogout);

            // Add controls into central panel in logical order
            flp.Controls.Add(lblWelcome);
            flp.Controls.Add(btnCreateRoom);
            flp.Controls.Add(lblJoin);
            flp.Controls.Add(txtRoomCode);
            flp.Controls.Add(btnJoinRoom);
            flp.Controls.Add(bottomRow);

            this.Controls.Add(flp);

            // Position after adding
            CenterContent();
        }

        private void CenterContent()
        {
            // Find the flow panel we added and center it
            foreach (Control c in this.Controls)
            {
                if (c is FlowLayoutPanel)
                {
                    var flp = (FlowLayoutPanel)c;
                    flp.Location = new Point((this.ClientSize.Width - flp.Width) / 2, (this.ClientSize.Height - flp.Height) / 2);
                    // locate bottom row (horizontal) if exists
                    FlowLayoutPanel bottom = null;
                    foreach (Control cc in flp.Controls)
                    {
                        if (cc is FlowLayoutPanel flpBottom && flpBottom.FlowDirection == FlowDirection.LeftToRight)
                        {
                            bottom = flpBottom;
                            break;
                        }
                    }

                    // adjust child sizes
                    foreach (Control child in flp.Controls)
                    {
                        if (child is TextBox tb)
                            tb.Width = Math.Max(200, flp.Width - 20);
                        if (child is Button btn && child.Parent == flp)
                            btn.Width = Math.Max(120, flp.Width - 20);
                    }

                    if (bottom != null)
                    {
                        bottom.Width = flp.Width;
                        // find logout button in bottom
                        Button logoutBtn = null;
                        foreach (Control b in bottom.Controls)
                        {
                            if (b is Button bb && bb.Text == "Đăng xuất") { logoutBtn = bb; break; }
                        }

                        if (logoutBtn != null)
                        {
                            // status occupies remaining width
                            foreach (Control b in bottom.Controls)
                            {
                                if (b == lblStatus)
                                {
                                    b.Width = bottom.Width - logoutBtn.Width - 12;
                                }
                            }
                        }
                        else
                        {
                            // fallback: make lblStatus full width
                            foreach (Control b in bottom.Controls)
                            {
                                if (b == lblStatus) b.Width = bottom.Width;
                            }
                        }
                    }
                }
            }
            // (logout now sits inside bottom row)
        }

        private void SubscribeEvents()
        {
            NetworkEvents.OnCreateRoomResponse += NetworkEvents_OnCreateRoomResponse;
            // OnJoinRoomResponse không subscribe ở đây nữa
            // vì cả 2 flow (join + create) đều dùng onceHandler riêng
        }

        private void LobbyForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            NetworkEvents.OnCreateRoomResponse -= NetworkEvents_OnCreateRoomResponse;
        }

        private void NetworkEvents_OnCreateRoomResponse(CreateRoomResponse payload)
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnCreateRoomResponse(payload)));
                return;
            }

            if (payload != null && payload.IsSuccess && !string.IsNullOrWhiteSpace(payload.RoomCode))
            {
                txtRoomCode.Text = payload.RoomCode;
                lblStatus.Text = $"Đã tạo phòng {payload.RoomCode}, đang vào phòng...";

                // ✅ FIX: Pre-create MainForm trước khi gửi JOIN_ROOM (giống flow join thủ công)
                var pendingMainForm = new MainForm(_network, payload.RoomCode, true);
                Action<JoinRoomResponse> onceHandler = null;
                onceHandler = (resp) =>
                {
                    NetworkEvents.OnJoinRoomResponse -= onceHandler;
                    if (resp != null && resp.IsSuccess)
                    {
                        pendingMainForm.SetRoomOwner(resp.IsRoomOwner);
                        pendingMainForm.RegisterUdpEndpoint();
                        this.BeginInvoke(new Action(() =>
                        {
                            this.Hide();
                            pendingMainForm.FormClosed += (fs, fe) => this.Close();
                            pendingMainForm.Show();
                        }));
                    }
                    else
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            pendingMainForm.Dispose();
                            lblStatus.Text = resp?.Message ?? "Không thể vào phòng";
                        }));
                    }
                };
                NetworkEvents.OnJoinRoomResponse += onceHandler;
                _network.SendJoinRoom(payload.RoomCode);
            }
            else
            {
                lblStatus.Text = payload?.Message ?? "Tạo phòng thất bại";
            }
        }

        private void NetworkEvents_OnJoinRoomResponse(JoinRoomResponse payload)
        {
            // Handler này chỉ còn dùng cho flow CreateRoom (tự động join sau khi tạo)
            // Flow JoinRoom thủ công đã dùng onceHandler riêng trong btnJoinRoom.Click
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnJoinRoomResponse(payload)));
                return;
            }

            if (payload != null && payload.IsSuccess)
            {
                lblStatus.Text = $"Đã vào phòng {payload.RoomCode}";
                MainForm mainForm = new MainForm(_network, payload.RoomCode, payload.IsRoomOwner);
                this.Hide();
                mainForm.FormClosed += (s, e) => this.Close();
                mainForm.Show();
            }
            else
            {
                lblStatus.Text = payload?.Message ?? "Không thể vào phòng";
            }
        }
    }
}
