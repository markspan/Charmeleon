using QRCoder;

namespace Charmeleon
{
    /// <summary>
    /// Shows the Web View URL as a QR code and plain text. When the machine has
    /// several local addresses (VPNs, virtual adapters), the dropdown lets the
    /// user pick which one to advertise; the QR code and URL update to match.
    /// </summary>
    public partial class WebDialog : Form
    {
        public WebDialog()
        {
            InitializeComponent();

            foreach (var ip in WebServer.Addresses())
                cboAddress.Items.Add(ip);

            cboAddress.SelectedIndexChanged += (s, e) => ShowSelected();
            if (cboAddress.Items.Count > 0) cboAddress.SelectedIndex = 0;   // fires ShowSelected
        }

        void ShowSelected()
        {
            string ip  = cboAddress.SelectedItem?.ToString() ?? "localhost";
            string url = WebServer.UrlFor(ip);
            lblUrl.Text = url;
            pictureBox1.Image?.Dispose();
            pictureBox1.Image = GenerateQR(url);
        }

        static Bitmap GenerateQR(string url)
        {
            using var gen  = new QRCodeGenerator();
            using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qr   = new QRCode(data);
            return qr.GetGraphic(6, Color.Black, Color.White, drawQuietZones: true);
        }
    }
}