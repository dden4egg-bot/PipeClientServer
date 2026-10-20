using System.IO.Pipes;
using System.Text;

namespace PipeClient
{
    public partial class ClientForm : Form
    {
        private NamedPipeClientStream _pipeClient;
        private StreamReader _reader;
        private StreamWriter _writer;
        private readonly string _pipeName = "TestPipe";

        // UI элементы
        private TextBox txtName;
        private Button btnConnect;
        private TextBox txtLog;
        private TextBox txtMessage;
        private Button btnSend;

        public ClientForm()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.Text = "Pipe Client";
            this.Size = new Size(450, 400);

            txtName = new TextBox { Left = 10, Top = 10, Width = 150, Text = "Client1" };
            btnConnect = new Button { Left = 170, Top = 8, Width = 100, Text = "Connect" };
            btnConnect.Click += BtnConnect_Click;

            txtLog = new TextBox { Left = 10, Top = 40, Width = 400, Height = 250, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

            txtMessage = new TextBox { Left = 10, Top = 300, Width = 300, Height = 25 };
            btnSend = new Button { Left = 320, Top = 298, Width = 90, Text = "Send" };
            btnSend.Click += BtnSend_Click;

            this.Controls.Add(txtName);
            this.Controls.Add(btnConnect);
            this.Controls.Add(txtLog);
            this.Controls.Add(txtMessage);
            this.Controls.Add(btnSend);
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _pipeClient.ConnectAsync(2000); // Ждем сервер 2 секунды

                _reader = new StreamReader(_pipeClient);
                _writer = new StreamWriter(_pipeClient) { AutoFlush = true };

                // Task 2: Отправляем серверу своё имя
                string name = txtName.Text;
                _writer.WriteLine(name);

                Log("Connected to server.");
                btnConnect.Enabled = false;
                txtName.Enabled = false;

                // Запускаем чтение от сервера в фоне
                _ = Task.Run(ReceiveLoop);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not connect: " + ex.Message);
            }
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (_pipeClient != null && _pipeClient.IsConnected)
                {
                    string message = await _reader.ReadLineAsync();
                    if (message == null) break;

                    if (message == "Ping")
                    {
                        // Task 1: Отвечаем на Ping
                        _writer.WriteLine("Pong");
                        Log("[Server] : Ping");
                    }
                    else
                    {
                        UpdateUI(() => Log($"[Server] : {message}"));
                    }
                }
            }
            catch
            {
                UpdateUI(() => Log("Disconnected from server."));
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            if (_pipeClient?.IsConnected == true && !string.IsNullOrWhiteSpace(txtMessage.Text))
            {
                try
                {
                    _writer.WriteLine(txtMessage.Text);
                    Log($"[Me] : {txtMessage.Text}");
                    txtMessage.Clear();
                }
                catch { }
            }
        }

        private void Log(string text)
        {
            UpdateUI(() => txtLog.AppendText($"{DateTime.Now:HH:mm:ss} {text}\r\n"));
        }

        private void UpdateUI(Action action)
        {
            if (InvokeRequired) Invoke(action);
            else action();
        }
    }
}