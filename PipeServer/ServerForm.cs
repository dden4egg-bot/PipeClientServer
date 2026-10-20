using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace PipeServer
{
    public partial class ServerForm : Form
    {
        // Словарь для хранения подключенных клиентов (Имя -> Поток для записи)
        private readonly ConcurrentDictionary<string, StreamWriter> _clients = new();
        private readonly string _pipeName = "TestPipe";
        private System.Windows.Forms.Timer _pingTimer;

        // Элементы UI (создадим кодом, чтобы не мучиться с дизайнером)
        private ListBox lstClients;
        private TextBox txtLog;
        private TextBox txtMessage;
        private Button btnSendAll;
        private Button btnSendSelected;

        public ServerForm()
        {
            InitializeComponent();
            SetupUI();
            StartServer();

            // Таймер для Task 1 (Ping каждые 5 секунд)
            _pingTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _pingTimer.Tick += PingTimer_Tick;
            _pingTimer.Start();
        }

        private void SetupUI()
        {
            this.Text = "Pipe Server";
            this.Size = new Size(600, 400);

            lstClients = new ListBox { Left = 10, Top = 10, Width = 200, Height = 300 };

            txtLog = new TextBox { Left = 220, Top = 10, Width = 350, Height = 250, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

            txtMessage = new TextBox { Left = 220, Top = 270, Width = 250, Height = 25 };

            btnSendAll = new Button { Left = 220, Top = 300, Width = 120, Height = 30, Text = "Send to All" };
            btnSendAll.Click += (s, e) => SendMessage(null);

            btnSendSelected = new Button { Left = 350, Top = 300, Width = 120, Height = 30, Text = "Send to Selected" };
            btnSendSelected.Click += (s, e) => SendMessage(lstClients.SelectedItem?.ToString());

            this.Controls.Add(lstClients);
            this.Controls.Add(txtLog);
            this.Controls.Add(txtMessage);
            this.Controls.Add(btnSendAll);
            this.Controls.Add(btnSendSelected);
        }

        private async void StartServer()
        {
            while (true)
            {
                var pipeServer = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync();

                // Обрабатываем клиента в отдельном потоке, чтобы не блокировать сервер
                _ = Task.Run(() => HandleClient(pipeServer));
            }
        }

        private async Task HandleClient(NamedPipeServerStream pipe)
        {
            using var reader = new StreamReader(pipe);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };

            try
            {
                // Первое сообщение от клиента - это его имя (Task 2)
                string clientName = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(clientName)) return;

                _clients.TryAdd(clientName, writer);
                UpdateUI(() => {
                    lstClients.Items.Add(clientName);
                    Log($"[System] Client {clientName} connected.");
                });

                // Читаем сообщения от клиента
                while (pipe.IsConnected)
                {
                    string message = await reader.ReadLineAsync();
                    if (message == null) break;

                    // Обработка Pong от клиента (Task 1)
                    if (message == "Pong")
                    {
                        UpdateUI(() => Log($"[{clientName}] : Pong"));
                    }
                    else
                    {
                        UpdateUI(() => Log($"[{clientName}] : {message}"));
                    }
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки разрыва соединения
            }
            finally
            {
                // Отключаем клиента
                string disconnectedName = _clients.FirstOrDefault(x => x.Value == writer).Key;
                if (disconnectedName != null)
                {
                    _clients.TryRemove(disconnectedName, out _);
                    UpdateUI(() => {
                        lstClients.Items.Remove(disconnectedName);
                        Log($"[System] Client {disconnectedName} disconnected.");
                    });
                }
                pipe.Dispose();
            }
        }

        // Task 1: Пинг каждые 5 секунд
        private void PingTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var client in _clients)
            {
                try
                {
                    client.Value.WriteLine("Ping");
                    UpdateUI(() => Log($"[Server -> {client.Key}] : Ping"));
                }
                catch { /* Ошибка отправки - клиент отвалился */ }
            }
        }

        // Task 4: Отправка сообщения либо всем, либо выбранному
        private void SendMessage(string? targetClient)
        {
            string msg = txtMessage.Text;
            if (string.IsNullOrWhiteSpace(msg)) return;

            if (targetClient == null)
            {
                // Всем
                foreach (var client in _clients)
                {
                    try { client.Value.WriteLine(msg); } catch { }
                }
                Log($"[Server -> All] : {msg}");
            }
            else
            {
                // Только выбранному
                if (_clients.TryGetValue(targetClient, out var writer))
                {
                    try { writer.WriteLine(msg); } catch { }
                    Log($"[Server -> {targetClient}] : {msg}");
                }
            }
            txtMessage.Clear();
        }

        // Вспомогательные методы для потокобезопасного обновления UI
        private void Log(string text)
        {
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} {text}\r\n");
        }

        private void UpdateUI(Action action)
        {
            if (InvokeRequired) Invoke(action);
            else action();
        }
    }
}