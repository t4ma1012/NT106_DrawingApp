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
        }

        private void BuildUi()
        {
            Label lblWelcome = new Label
            {
                Text = $"Xin chào, {_username}",
                AutoSize = true,
                Location = new Point(20, 20),
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };

            Button btnCreateRoom = new Button
            {
                Text = "Tạo phòng mới",
                Size = new Size(190, 36),
                Location = new Point(20, 65)
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
                Location = new Point(20, 130)
            };

            txtRoomCode = new TextBox
            {
                Location = new Point(20, 153),
                Width = 190
            };

            Button btnJoinRoom = new Button
            {
                Text = "Tham gia phòng",
                Size = new Size(190, 36),
                Location = new Point(20, 186)
            };
            btnJoinRoom.Click += (s, e) =>
            {
                string roomCode = txtRoomCode.Text?.Trim();
                if (string.IsNullOrWhiteSpace(roomCode))
                {
                    MessageBox.Show("Vui lòng nhập mã phòng.");
                    return;
                }

                lblStatus.Text = "Đang gửi yêu cầu tham gia...";
                _network.SendJoinRoom(roomCode);
            };

            lblStatus = new Label
            {
                AutoSize = false,
                Width = 390,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(20, 230),
                ForeColor = Color.DimGray,
                Text = "Sẵn sàng"
            };

            this.Controls.Add(lblWelcome);
            this.Controls.Add(btnCreateRoom);
            this.Controls.Add(lblJoin);
            this.Controls.Add(txtRoomCode);
            this.Controls.Add(btnJoinRoom);
            this.Controls.Add(lblStatus);
        }

        private void SubscribeEvents()
        {
            NetworkEvents.OnCreateRoomResponse += NetworkEvents_OnCreateRoomResponse;
            NetworkEvents.OnJoinRoomResponse += NetworkEvents_OnJoinRoomResponse;
        }

        private void LobbyForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            NetworkEvents.OnCreateRoomResponse -= NetworkEvents_OnCreateRoomResponse;
            NetworkEvents.OnJoinRoomResponse -= NetworkEvents_OnJoinRoomResponse;
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
                _network.SendJoinRoom(payload.RoomCode);
            }
            else
            {
                lblStatus.Text = payload?.Message ?? "Tạo phòng thất bại";
            }
        }

        private void NetworkEvents_OnJoinRoomResponse(JoinRoomResponse payload)
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnJoinRoomResponse(payload)));
                return;
            }

            if (payload != null && payload.IsSuccess)
            {
                lblStatus.Text = $"Đã vào phòng {payload.RoomCode}";
                MainForm mainForm = new MainForm(_network, payload.RoomCode);
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