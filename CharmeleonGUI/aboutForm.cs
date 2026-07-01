namespace Charmeleon
{
    /// <summary>About dialog showing the Charmeleon logo.</summary>
    public partial class aboutForm : Form
    {
        /// <summary>Loads the logo image (ignored if the file is missing).</summary>
        public aboutForm()
        {
            InitializeComponent();
            try { pictureBox1.Image = Image.FromFile("Resources/Charmeleon.png"); }
            catch { }
        }

    }
}

