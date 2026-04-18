using System;
using System.Drawing;
using System.Windows.Forms;
using DrawingClient.Network;
using SharedLib.Payloads;

namespace DrawingClient.Forms
{
    public class LoginForm : Form
    {
        private readonly ClientNetwork _network;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtServer;
        private Label lblStatus;

        public LoginForm()
        {
            _network = new ClientNetwork();
            txtUsername = new TextBox();
            this.Text = "Đăng nhập";
            this.Size = new Size(420, 290);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            BuildUi();
            SubscribeEvents();
            this.FormClosed += LoginForm_FormClosed;
        }

        private void BuildUi()
        {
            Label lblTitle = new Label
            {
                Text = "Drawing App - Login",
                AutoSize = true,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                Location = new Point(20, 18)
            };

            Label lblServer = new Label { Text = "Server:", AutoSize = true, Location = new Point(20, 65) };
            txtServer = new TextBox { Location = new Point(120, 60), Width = 250, Text = "127.0.0.1" };

            Label lblUser = new Label { Text = "Tài khoản:", AutoSize = true, Location = new Point(20, 102) };
            txtUsername = new TextBox { Location = new Point(120, 97), Width = 250 };

            Label lblPass = new Label { Text = "Mật khẩu:", AutoSize = true, Location = new Point(20, 139) };
            txtPassword = new TextBox { Location = new Point(120, 134), Width = 250, UseSystemPasswordChar = true };

            Button btnLogin = new Button
            {
                Text = "Đăng nhập",
                Location = new Point(120, 178),
                Size = new Size(120, 34)
            };
            btnLogin.Click += BtnLogin_Click;

            Button btnRegister = new Button
            {
                Text = "Đăng ký",
                Location = new Point(250, 178),
                Size = new Size(120, 34)
            };
            btnRegister.Click += (s, e) =>
            {
                if (!EnsureConnected())
                    return;

                _network.SendRegister(txtUsername.Text.Trim(), txtPassword.Text);
            };

            lblStatus = new Label
            {
                AutoSize = false,
                Width = 350,
                Height = 28,
                Location = new Point(20, 220),
                ForeColor = Color.DimGray,
                Text = "Sẵn sàng"
            };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblServer);
            this.Controls.Add(txtServer);
            this.Controls.Add(lblUser);
            this.Controls.Add(txtUsername);
            this.Controls.Add(lblPass);
            this.Controls.Add(txtPassword);
            this.Controls.Add(btnLogin);
            this.Controls.Add(btnRegister);
            this.Controls.Add(lblStatus);
        }

        private void SubscribeEvents()
        {
            NetworkEvents.OnLoginResponse += NetworkEvents_OnLoginResponse;
            NetworkEvents.OnRegisterResponse += NetworkEvents_OnRegisterResponse;
            NetworkEvents.OnDisconnected += NetworkEvents_OnDisconnected;
        }

        private void LoginForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            NetworkEvents.OnLoginResponse -= NetworkEvents_OnLoginResponse;
            NetworkEvents.OnRegisterResponse -= NetworkEvents_OnRegisterResponse;
            NetworkEvents.OnDisconnected -= NetworkEvents_OnDisconnected;
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text?.Trim();
            string password = txtPassword.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Vui lòng nhập tài khoản và mật khẩu.");
                return;
            }

            if (!EnsureConnected())
                return;

            lblStatus.Text = "Trang gửi thông tin xác thực...";
            _network.SendLogin(username, password);
        }

        private bool EnsureConnected()
        {
            if (_network.IsConnected)
                return true;

            string serverIp = txtServer.Text?.Trim();
            if (string.IsNullOrWhiteSpace(serverIp))
                serverIp = "127.0.0.1";

            bool connected = _network.Connect(serverIp, 8888, true);
            if (!connected)
            {
                lblStatus.Text = "Không thể kết nối máy chủ.";
                return false;
            }

            return true;
        }

        private void NetworkEvents_OnLoginResponse(LoginResponse response)
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnLoginResponse(response)));
                return;
            }

            if (response != null && response.IsSuccess)
            {
                lblStatus.Text = "Đăng nhập thành công.";
                LobbyForm lobby = new LobbyForm(_network, response.Username ?? txtUsername.Text.Trim());
                this.Hide();
                lobby.FormClosed += (s, e) => this.Close();
                lobby.Show();
            }
            else
            {
                lblStatus.Text = response?.Message ?? "Đăng nhập thất bại.";
            }
        }

        private void NetworkEvents_OnRegisterResponse(RegisterResponse response)
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => NetworkEvents_OnRegisterResponse(response)));
                return;
            }

            lblStatus.Text = response?.Message ?? "Đăng ký xong.";
        }

        private void NetworkEvents_OnDisconnected()
        {
            if (this.IsHandleCreated && this.InvokeRequired)
            {
                this.BeginInvoke(new Action(NetworkEvents_OnDisconnected));
                return;
            }

            lblStatus.Text = "Mất kết nối máy chủ.";
        }
    }
}