using System;
using System.Text;
using System.Windows;
using System.IO.Ports;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace Serial_CartDumper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialPort serialPort = new SerialPort();
        private StringBuilder sb = new StringBuilder();
        private StringBuilder tb = new StringBuilder();
        private System.IO.MemoryStream binaryData = new System.IO.MemoryStream();
        private bool receivingDumpData = false;

        public MainWindow()
        {
            InitializeComponent();
            //ListSerialPorts();
            Task taskA = Task.Run( () => SerialCheck());
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if(serialPort==null) return;
            if(!serialPort.IsOpen) return;

            string data = serialPort.ReadExisting();
            GuiLog(data);
            
            sb.Append(data);
            int i = sb.ToString().IndexOf("\r\n");
            while(i>=0){
                var line = sb.ToString().Substring(0, i+2);
                OnDataLine(line.Trim());
                sb.Remove(0, i+2);
                i = sb.ToString().IndexOf("\r\n");
            }

        }

        private void OnDataLine(string line)
        {
            try
            {
                if(line==null || line.Length==0) return;
                if(line.EndsWith(" >>> BASE64 DUMP END")){
                    string ext = GetFileType(line);
                    OutputData(ext);
                    receivingDumpData = false;
                }
                if(receivingDumpData){
                    //base64 -> byte[]
                    //byte[64] split data
                    binaryData.Write(Convert.FromBase64String(line));
                }
                if(line.StartsWith("BASE64 DUMP START <<< ")){
                    binaryData.SetLength(0);
                    receivingDumpData = true;
                }
            }
            catch (Exception)
            {
                binaryData.SetLength(0);
                receivingDumpData = false;
            }
        }

        private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Console.WriteLine(e);
        }

        private void ComboBoxPorts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerialClose();
            SerialOpen();
        }

        private void OutputData(string ext)
        {
            byte[] data = binaryData.ToArray();
            binaryData.SetLength(0);

            Dispatcher.InvokeAsync((Action)(() =>
            {
                try
                {
                    string fileName = "CART." + ext;
                    System.IO.File.WriteAllBytes(fileName, data);
                    GuiDialog("output " + fileName);
                }
                catch (Exception)
                {
                    GuiDialog("output Error!");
                }
            }));
        }

        private void GuiDialog(string msg)
        {
            Dispatcher.InvokeAsync((Action)(() =>
            {
                MessageBox.Show(msg);
            }));
        }

        private void GuiLog(string msg)
        {
            lock(TextBlockLog){
                tb.Append(msg);
                if(tb.Length>16*1024){
                    tb.Remove(0, 4*1024);
                    string s = tb.ToString();
                    tb.Clear();
                    tb.Append(s);
                }
            }

            Dispatcher.Invoke((Action)(() =>
            {
                lock(TextBlockLog){
                    TextBlockLog.Text = tb.ToString();
                }
                TextBlockLog.ScrollToEnd();
            }));
        }

        private void SerialCheck()
        {
            while(true){
                System.Threading.Thread.Sleep(3000);
                try{
                    if(serialPort!=null && !serialPort.IsOpen){
                        //SerialClose();
                        SerialOpen();
                    }
                }catch(Exception e){
                }
            }
        }

        private void SerialOpen()
        {
            try
            {
                serialPort = new SerialPort();
                serialPort.PortName = GetPort();
                serialPort.BaudRate = 115200;
                serialPort.DataBits = 8;
                serialPort.NewLine = "\r\n";
                serialPort.Parity = Parity.None;
                serialPort.StopBits = StopBits.One;
                serialPort.DataReceived += OnDataReceived;
                serialPort.ErrorReceived += OnErrorReceived;
                serialPort.RtsEnable = true;
                serialPort.DtrEnable = true;
                serialPort.Open();
            }
            catch (Exception ex)
            {
                //GuiDialog(ex.Message);
            }
        }

        public void SerialClose()
        {
            try
            {
                if(serialPort!=null){
                    serialPort.Close();
                }
            }
            catch (Exception ex)
            {
            }
        }

        private string GetFileType(string msg)
        {
            string type = msg.Substring(0, msg.IndexOf(" >>> "));
            switch (type)
            {
                case "NES":
                    return "nes";
                case "GB":
                    return "gb";
                case "NES|SAVE":
                    return "bin";
                case "GB|SAVE":
                    return "sav";
                case "GBA|SAVE":
                    return "srm";
                case "SNES|SAVE":
                    return "srm";
                case "N64|SAVE4":
                    return "fla";
                case "N64|SAVE1":
                    return "sra";
                default:
                    return "bin";
            }
        }

        private string GetPort()
        {
            string port = null;
            Dispatcher.Invoke((Action)(() =>
            {
                port = ComboBoxPorts.SelectedValue.ToString();
            }));

            return port ?? "COM1";
        }

        private void ListSerialPorts()
        {
            ComboBoxPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                ComboBoxPorts.Items.Add(port);
                Console.WriteLine(port);
            }
            ComboBoxPorts.SelectedIndex = 0;
        }
    }
}
