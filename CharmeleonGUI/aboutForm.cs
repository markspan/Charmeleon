namespace Charmeleon
{
    public partial class aboutForm : Form
    {
        public aboutForm()
        {
            InitializeComponent();
            try { pictureBox1.Image = Image.FromFile("Resources/Charmeleon.png"); }
            catch { }
        }

    }
}

