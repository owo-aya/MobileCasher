using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MobileCasher {
internal sealed class LanServer : IDisposable {
    readonly TcpListener listener;
    readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
    readonly Queue<KeyValuePair<long,string>> history = new Queue<KeyValuePair<long,string>>();
    long sequence;
    long lastPoll;
    volatile bool stopped;
    public bool Connected => DateTime.UtcNow.Ticks - Interlocked.Read(ref lastPoll) < TimeSpan.FromSeconds(5).Ticks;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public LanServer(IPAddress address = null, int port = 47635) {
        foreach (var file in new[] { "index.html", "receipt.css", "receipt.js", "live.css", "live.js",
                     "assets/unknown.svg", "assets/transfer-rounded.ttf" })
            files["/" + file] = ReadPhoneResource(file);
        files["/"] = files["/index.html"];
        var clipNames = new List<string>();
        var tokens = new[] {
            "tts_0", "tts_1", "tts_2", "tts_3", "tts_4", "tts_5", "tts_6", "tts_7", "tts_8", "tts_9",
            "tts_dot", "tts_ten", "tts_hundred", "tts_thousand", "tts_ten_thousand", "tts_ten_million",
            "tts_yuan", "tts_success", "wx_success", "wx_chime", "wx_prefix"
        };
        foreach (var token in tokens) {
            var audio = ModHelper.ReadModFileBytes("announce_the_transaction", "Sounds/" + token + ".mp3");
            if (audio == null) throw new InvalidOperationException("前置模组缺少语音文件：" + token + ".mp3");
            files["/sounds/" + token + ".mp3"] = audio;
            clipNames.Add("\"" + token + "\"");
        }
        files["/clips"] = Encoding.UTF8.GetBytes("[" + string.Join(",",clipNames) + "]");
        listener = new TcpListener(address ?? IPAddress.Any, port);
        try { listener.Start(); }
        catch (SocketException) { listener = new TcpListener(address ?? IPAddress.Any, 0); listener.Start(); }
        new Thread(Accept) { IsBackground = true, Name = "MobileCasher HTTP" }.Start();
    }
    static byte[] ReadPhoneResource(string file) {
        string name = "MobileCasher.Phone." + file.Replace('/', '.');
        using (var stream = typeof(LanServer).Assembly.GetManifestResourceStream(name)) {
            if (stream == null) throw new InvalidOperationException("缺少内嵌手机页面资源：" + file);
            using (var output = new MemoryStream()) {
                stream.CopyTo(output);
                return output.ToArray();
            }
        }
    }
    public void Publish(string json) {
        lock (history) {
            history.Enqueue(new KeyValuePair<long,string>(++sequence, json));
            if (history.Count > 64) history.Dequeue();
        }
    }
    void Accept() {
        while (!stopped) {
            try {
                var client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => Handle(client));
            } catch (SocketException) { if (stopped) break; }
              catch (ObjectDisposedException) { break; }
        }
    }
    void Handle(TcpClient client) {
        using (client) try {
            client.ReceiveTimeout = client.SendTimeout = 2500;
            var stream = client.GetStream();
            // Read just the request line, with a fixed upper bound.
            var line = new StringBuilder();
            while (line.Length < 1024) {
                int ch = stream.ReadByte();
                if (ch < 0) return;
                if (ch == 10) break;
                line.Append((char)ch);
            }
            var fields = line.ToString().Split(' ');
            if (fields.Length != 3 || fields[0] != "GET") return;
            int headerBytes = 0, consecutive = 1;
            while (consecutive < 2) {
                int ch = stream.ReadByte();
                if (ch < 0 || ++headerBytes > 16384) return;
                if (ch == 10) consecutive++;
                else if (ch != 13) consecutive = 0;
            }
            string route = fields[1], mime = "application/json";
            byte[] body;
            string status = "200 OK";
            if (route == "/cursor") {
                lock (history) body = Encoding.UTF8.GetBytes(sequence.ToString());
            } else if (route.StartsWith("/events?after=", StringComparison.Ordinal)
                       && long.TryParse(route.Substring(14), out long after)) {
                Interlocked.Exchange(ref lastPoll, DateTime.UtcNow.Ticks);
                lock (history) {
                    var messages = new List<string>();
                    if (after > sequence) after = 0;
                    foreach (var pair in history)
                        if (pair.Key > after) messages.Add(pair.Value);
                    body = Encoding.UTF8.GetBytes("{\"cursor\":" + sequence + ",\"items\":[" + string.Join(",", messages) + "]}");
                }
            } else if (files.TryGetValue(route, out body)) {
                switch (Path.GetExtension(route)) {
                    case ".js": mime = "application/javascript"; break;
                    case ".css": mime = "text/css"; break;
                    case ".mp3": mime = "audio/mpeg"; break;
                    case ".png": mime = "image/png"; break;
                    case ".svg": mime = "image/svg+xml"; break;
                    case ".ttf": mime = "font/ttf"; break;
                    default: mime = route == "/clips" ? "application/json" : "text/html; charset=utf-8"; break;
                }
            } else { status = "404 Not Found"; mime = "text/plain"; body = new byte[0]; }
            var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: " + mime +
                "\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            stream.Write(header,0,header.Length);
            stream.Write(body,0,body.Length);
        } catch (IOException) { } catch (SocketException) { }
    }
    public void Dispose() { stopped = true; listener.Stop(); }
}
}
