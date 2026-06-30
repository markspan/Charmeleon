using QRCoder;

namespace Charmeleon
{
    /// <summary>
    /// Shows the Web View URL as a QR code and plain text so any device
    /// on the lab network can open the live impedance head map in a browser.
    /// </summary>
    public partial class WebDialog : Form
    {
        public WebDialog()
        {
            InitializeComponent();
            string url = WebServer.Url;
            lblUrl.Text = url;
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
