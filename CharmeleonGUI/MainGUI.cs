using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;

namespace CharmeleonGUI
{
    public partial class MainGUI : Form
    {
        RtDeviceDriver.RefaDriver Amplifier;
        int centerX = 0;
        int centerY = 0;
        // Get form size
        int formHeight = 0;
        int formWidth = 0;

        // Define the circle radius (90% of form height)
        int fullRadius = 0;

        public Dictionary<string, ElectrodeControl> electrodes = new();
        //ElectrodeControl[] electrodes;
        public Dictionary<string, (double Angle, double Radius)> electrodePositions = new()
        {
            // Midline electrodes
            { "Nz", (90, 5) }, { "FPz", (90, 4) }, { "AFz", (90, 3) }, { "Fz", (90, 2) }, { "FCz", (90, 1) },
            { "Cz", (0, 0) }, { "CPz", (-90, 1) }, { "Pz", (-90, 2) }, { "POz", (-90, 3) },
            { "Oz", (-90, 4) }, { "Iz", (-90, 5) },
            // C-line electrodes
            { "T7",(180, 4) }, { "C5",(180, 3) }, { "C3",(180, 2) }, { "C1",(180, 1) },  { "C2",(0, 1) },
            { "C4",(0, 2) },  { "C6",(0, 3) },  { "T8",(0, 4) }, {"A2" , (0,5) }, { "A1", (180, 5) },
            // Outer Circle
            {  "F9",(144, 5) }, {  "FT9",(162, 5) }, {  "TP9",(198, 5) }, {  "P9",(216, 5) },
            { "F10",( 36, 5) }, { "FT10",( 18, 5) }, { "TP10",( -18, 5) }, { "P10",( -36, 5) },
            // First Circle
            { "Fp1",(108, 4) }, { "AF7",(126, 4) }, { "F7",(144, 4) }, { "FT7",(162, 4) },
            { "Fp2",( 72, 4) }, { "AF8",( 54, 4) }, { "F8",( 36, 4) }, { "FT8",( 18, 4) },
            { "O1", (-108, 4) }, { "PO7",(-126, 4) }, { "P7",(-144, 4) }, { "TP7",(-162, 4) },
            { "O2", ( -72, 4) }, { "PO8",( -54, 4) }, { "P8",( -36, 4) }, { "TP8",( -18, 4) },
            // Others
            { "AF4", (  68, 3.2) }, { "F6", (  43, 3.1) }, { "F4", (  55, 2.5 )}, { "F2", (  70, 2.15) },
            { "AF3", ( 112, 3.2) }, { "F5", ( 137, 3.1) }, { "F3", ( 125, 2.5 )}, { "F1", ( 110, 2.15) },
            { "PO4", ( -68, 3.2) }, { "P6", ( -43, 3.1) }, { "P4", ( -55, 2.45)}, { "P2", ( -70, 2.15) },
            { "PO3", (-112, 3.2) }, { "P5", (-137, 3.1) }, { "P3", (-125, 2.45)}, { "P1", (-110, 2.15) },
            // More others
            { "FC5",(158, 3.05) }, { "FC3",(151, 2.1) }, { "FC1",(135, 1.35) },
            { "CP5",(-158, 3.05)}, { "CP3",(-151, 2.1)}, { "CP1",(-135, 1.35)},
            { "FC6",(  22, 3.05)}, { "FC4",(  29, 2.1)}, { "FC2",( 45, 1.35) },
            { "CP6",( -22, 3.05)}, { "CP4",( -29, 2.1)}, { "CP2",( -45, 1.35)},
         };

        public Dictionary<string, Point> AUXPositions = new()
        {
            { "Left",  new Point()},
            { "Right", new Point()},
            { "Top",   new Point()},
            { "Bottom",new Point()}
        };

        public MainGUI()
        {
            InitializeComponent();
            this.formHeight = this.ClientSize.Height;
            this.formWidth = this.ClientSize.Width; 
            this.fullRadius = (int)(this.formHeight * 0.43); ;
            this.centerX = formWidth / 2;
            this.centerY = formHeight / 2;
            
            AUXPositions["Left"]  = new Point((int)(this.formWidth * .85), (int)(this.formHeight * .8));
            AUXPositions["Right"] = new Point((int)(this.formWidth * .85), (int)(this.formHeight * .72));
            AUXPositions["Top"]   = new Point((int)(this.formWidth * .90), (int)(this.formHeight * .8));
            AUXPositions["Bottom"]= new Point((int)(this.formWidth * .90), (int)(this.formHeight * .72));

            foreach (var AUX in AUXPositions)
            {
                ElectrodeControl electrode = new ElectrodeControl();
                electrode.LabelText = AUX.Key;
                electrode.Location = AUX.Value;
                this.Controls.Add(electrode);
                this.electrodes.Add(AUX.Key, electrode);
            }

            this.DoubleBuffered = true; // To avoid flickering during repainting
            AddElectrodes();
        }

        
        void AddElectrodes()
        {
            foreach (var (name, (angle, radius)) in electrodePositions)
            {
                ElectrodeControl electrode = new ElectrodeControl();
                double radians = angle * Math.PI / 180.0;

                int x = this.centerX + (int)(Math.Cos(radians) * (radius * (this.fullRadius / 5.0)));
                int y = this.centerY - (int)(Math.Sin(radians) * (radius * (this.fullRadius / 5.0)));
                electrode.LabelText = name;
                electrode.Location = new Point(x-(electrode.Width/2), y-(electrode.Height / 2));
                this.Controls.Add(electrode);
                this.electrodes.Add(name, electrode);
            }

        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;

            // Create a Pen for the circle outline
            Pen circlePen = new Pen(Color.Gray, 2); // Blue color, 2px width
            Pen innercirclePen = new Pen(Color.Gray, 1); // Blue color, 2px width
            innercirclePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            // Draw the circle (Centered)
            g.DrawEllipse(circlePen, this.centerX - this.fullRadius, this.centerY - this.fullRadius, 2 * this.fullRadius, 2 * this.fullRadius);
            int circleRadiusn = (this.fullRadius * 4) / 5;
            g.DrawEllipse(innercirclePen, this.centerX - circleRadiusn, this.centerY - circleRadiusn, 2 * circleRadiusn, 2 * circleRadiusn);
            circleRadiusn = (this.fullRadius * 3) / 5;
            g.DrawEllipse(innercirclePen, this.centerX - circleRadiusn, this.centerY - circleRadiusn, 2 * circleRadiusn, 2 * circleRadiusn);
            circleRadiusn = (this.fullRadius * 2) / 5;
            g.DrawEllipse(innercirclePen, this.centerX - circleRadiusn, this.centerY - circleRadiusn, 2 * circleRadiusn, 2 * circleRadiusn);
            circleRadiusn = this.fullRadius / 5;
            g.DrawEllipse(innercirclePen, this.centerX - circleRadiusn, this.centerY - circleRadiusn, 2 * circleRadiusn, 2 * circleRadiusn);

            // Draw the cross (dotted grey lines)
            Pen dottedPen = new Pen(Color.Gray)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dot
            };

            // Horizontal line (centered at the form)
            g.DrawLine(dottedPen, 0, this.centerY, (int)(formHeight * 1.125), this.centerY);
            // Vertical line (centered at the form)
            g.DrawLine(dottedPen, this.centerX, 0, this.centerX, formHeight);

            // Ears:
            Point L = this.electrodes["A1"].Location;
            Point R = this.electrodes["A2"].Location;
            int RadX = this.fullRadius / 14;
            int RadY = this.fullRadius / 7;

            g.DrawEllipse(circlePen, L.X - 20, L.Y - 20, 2 * RadX, 2 * RadY);
            g.DrawEllipse(circlePen, R.X + 18, R.Y - 20, 2 * RadX, 2 * RadY);


        }
    }
}
