using System;
using System.Windows.Forms;
using DrawingClient.Network;

namespace DrawingClient
{
    public partial class Form1 : Form
    {
        // Khai báo công cụ mạng của người B
        private ClientNetwork _network;

        public Form1()
        {
            //InitializeComponent();

            // Gọi hàm test kết nối ngay khi Form vừa khởi tạo xong
            TestConnection();
        }

        private void TestConnection()
        {
            try
            {
                _network = new ClientNetwork();

                // Gọi hàm Connect theo đúng chuẩn file tài liệu của người B
                bool isConnected = _network.Connect("127.0.0.1", 8888);

                if (isConnected)
                {
                    MessageBox.Show("Tuyệt vời! Đã kết nối thành công tới Drawing Server!", "Thông báo");

                    // Thử gửi một gói tin Đăng nhập
                    _network.SendLogin("Trung_Test", "123456");
                }
                else
                {
                    MessageBox.Show("Kết nối thất bại. Nhớ bật Server lên trước nha!", "Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi rùi: " + ex.Message, "Lỗi báo động");
            }
        }
    }
}