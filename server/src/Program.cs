using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace cafiine_server
{
    class Program
    {
        public const byte BYTE_NORMAL = 0xff;
        public const byte BYTE_SPECIAL = 0xfe;
        public const byte BYTE_OPEN = 0x00;
        public const byte BYTE_READ = 0x01;
        public const byte BYTE_CLOSE = 0x02;
        public const byte BYTE_OK = 0x03;
        public const byte BYTE_SETPOS = 0x04;
        public const byte BYTE_STATFILE = 0x05;
        public const byte BYTE_EOF = 0x06;
        public const byte BYTE_GETPOS = 0x07;
        public const byte BYTE_REQUEST = 0x08;
        public const byte BYTE_REQUEST_SLOW = 0x09;
        public const byte BYTE_HANDLE = 0x0A;
        public const byte BYTE_DUMP = 0x0B;
        public const byte BYTE_PING = 0x0C;

        [Flags]
        public enum FSStatFlag : uint
        {
            None = 0,
            unk_14_present = 0x01000000,
            mtime_present = 0x04000000,
            ctime_present = 0x08000000,
            entid_present = 0x10000000,
            directory = 0x80000000,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FSStat
        {
            public FSStatFlag flags;
            public uint permission;
            public uint owner;
            public uint group;
            public uint file_size;
            public uint unk_14_nonzero;
            public uint unk_18_zero;
            public uint unk_1c_zero;
            public uint ent_id;
            public uint ctime_u;
            public uint ctime_l;
            public uint mtime_u;
            public uint mtime_l;
            public uint unk_34_zero;
            public uint unk_38_zero;
            public uint unk_3c_zero;
            public uint unk_40_zero;
            public uint unk_44_zero;
            public uint unk_48_zero;
            public uint unk_4c_zero;
            public uint unk_50_zero;
            public uint unk_54_zero;
            public uint unk_58_zero;
            public uint unk_5c_zero;
            public uint unk_60_zero;
        }

        public static string root = "cafiine_root";
        public static string logs_root = "logs";
        
        static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                Console.Error.WriteLine("Usage: cafiine_server [rootdir]");
                return;
            }
            if (args.Length == 1)
            {
                root = args[0];
            }
            // Check for cafiine_root and logs folder
            if (!Directory.Exists(root))
            {
                Console.Error.WriteLine("Root directory `{0}' does not exist!", root);
                return;
            }
            if (!Directory.Exists(logs_root))
            {
                Console.Error.WriteLine("Logs directory `{0}' does not exist!", logs_root);
                return;
            }
            // Delete logs
            System.IO.DirectoryInfo downloadedMessageInfo = new DirectoryInfo(logs_root);
            foreach (FileInfo file in downloadedMessageInfo.GetFiles())
            {
                file.Delete();
            }

            // Start server
            string name = "[listener]";
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, 7332);
                listener.Start();
                Console.WriteLine(name + " Listening on 7332");

                int index = 0;
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("connected");
                    Thread thread = new Thread(Handle);
                    thread.Name = "[" + index.ToString() + "]";
                    thread.Start(client);
                    index++;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(name + " " + e.Message);
            }
            Console.WriteLine(name + " Exit");
        }

        static void Log(StreamWriter log, String str)
        {
            log.WriteLine(str);
            log.Flush();
            Console.WriteLine(str);
        }

        static void Handle(object client_obj)
        {
            string name = Thread.CurrentThread.Name;
            FileStream[] files = new FileStream[256];
            Dictionary<int, FileStream> files_request = new Dictionary<int, FileStream>();
            StreamWriter log = null;

            try
            {
                TcpClient client = (TcpClient)client_obj;
                using (NetworkStream stream = client.GetStream())
                {
                    EndianBinaryReader reader = new EndianBinaryReader(stream);
                    EndianBinaryWriter writer = new EndianBinaryWriter(stream);

                    uint[] ids = reader.ReadUInt32s(4);

                    // Log connection
                    Console.WriteLine(name + " Accepted connection from client " + client.Client.RemoteEndPoint.ToString());
                    Console.WriteLine(name + " TitleID: " + ids[0].ToString("X8") + "-" + ids[1].ToString("X8"));

                    if (!Directory.Exists(root + "\\" + ids[0].ToString("X8") + "-" + ids[1].ToString("X8")))
                    {
                        writer.Write(BYTE_NORMAL);
                        throw new Exception("Not interested.");
                    }

                    // Create log file for current thread
                    log = new StreamWriter(logs_root + "\\" + DateTime.Now.ToString("yyyy-MM-dd") + "-" + name + "-" + ids[0].ToString("X8") + "-" + ids[1].ToString("X8") + ".txt");
                    log.WriteLine(name + " Accepted connection from client " + client.Client.RemoteEndPoint.ToString());
                    log.WriteLine(name + " TitleID: " + ids[0].ToString("X8") + "-" + ids[1].ToString("X8"));

                    string LocalRoot = root + "\\" + ids[0].ToString("X8") + "-" + ids[1].ToString("X8");
                    writer.Write(BYTE_SPECIAL);

                    while (true)
                    {
                        byte cmd_byte = reader.ReadByte();
                        switch (cmd_byte)
                        {
                            case BYTE_OPEN:
                                {
                                    bool request_slow = false;

                                    int len_path = reader.ReadInt32();
                                    int len_mode = reader.ReadInt32();
                                    string path = reader.ReadString(Encoding.ASCII, len_path - 1);
                                    if (reader.ReadByte() != 0) throw new InvalidDataException();
                                    string mode = reader.ReadString(Encoding.ASCII, len_mode - 1);
                                    if (reader.ReadByte() != 0) throw new InvalidDataException();
                                    Log(log, name + " " + path);
                                    if (File.Exists(LocalRoot + path))
                                    {
                                        int handle = -1;
                                        for (int i = 0; i < files.Length; i++)
                                        {
                                            if (files[i] == null)
                                            {
                                                handle = i;
                                                break;
                                            }
                                        }
                                        if (handle == -1)
                                        {
                                            Log(log, name + " Out of file handles!");
                                            writer.Write(BYTE_SPECIAL);
                                            writer.Write(-19);
                                            writer.Write(0);
                                            break;
                                        }
                                        Log(log, name + " -> fopen(\"" + path + "\", \"" + mode + "\") = " + handle.ToString());

                                        files[handle] = new FileStream(LocalRoot + path, FileMode.Open, FileAccess.Read, FileShare.Read);

                                        writer.Write(BYTE_SPECIAL);
                                        writer.Write(0);
                                        writer.Write(0x0fff00ff | (handle << 8));

                                    }
                                    // Check for file request : filename + "-request" or + "-request_slow"
                                    else if (File.Exists(LocalRoot + path + "-request") || (request_slow = File.Exists(LocalRoot + path + "-request_slow")))
                                    {
                                        // Check if dump has already been done
                                        if (!File.Exists(LocalRoot + path + "-dump"))
                                        {
                                            // Inform cafiine that we request file to be sent
                                            writer.Write(!request_slow ? BYTE_REQUEST : BYTE_REQUEST_SLOW);
                                        }
                                        else
                                        {
                                            // Nothing to do
                                            writer.Write(BYTE_NORMAL);
                                        }
                                    }
                                    else
                                    {
                                        writer.Write(BYTE_NORMAL);
                                    }
                                    break;
                                }
                            case BYTE_HANDLE:
                                {
                                    // Read buffer params : fd, path length, path string
                                    int fd = reader.ReadInt32();
                                    int len_path = reader.ReadInt32();
                                    string path = reader.ReadString(Encoding.ASCII, len_path - 1);
                                    if (reader.ReadByte() != 0) throw new InvalidDataException();

                                    // Add new file for incoming data
                                    files_request.Add(fd, new FileStream(LocalRoot + path + "-dump", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write));

                                    // Send response
                                    writer.Write(BYTE_SPECIAL);
                                    break;
                                }
                            case BYTE_DUMP:
                                {
                                    // Read buffer params : fd, size, file data
                                    int fd = reader.ReadInt32();
                                    int sz = reader.ReadInt32();
                                    byte[] buffer = new byte[sz];
                                    buffer = reader.ReadBytes(sz);

                                    // Look for file descriptor
                                    foreach (var item in files_request)
                                    {
                                        if (item.Key == fd)
                                        {
                                            FileStream dump_file = item.Value;
                                            if (dump_file == null)
                                                break;

                                            Log(log, name + " -> dump(\"" + Path.GetFileName(dump_file.Name) + "\") " + (sz / 1024).ToString() + "kB");

                                            // Write to file
                                            dump_file.Write(buffer, 0, sz);

                                            break;
                                        }
                                    }

                                    // Send response
                                    writer.Write(BYTE_SPECIAL);
                                    break;
                                }
                            case BYTE_READ:
                                {
                                    int size = reader.ReadInt32();
                                    int count = reader.ReadInt32();
                                    int fd = reader.ReadInt32();
                                    if ((fd & 0x0fff00ff) == 0x0fff00ff)
                                    {
                                        int handle = (fd >> 8) & 0xff;
                                        if (files[handle] == null)
                                        {
                                            writer.Write(BYTE_SPECIAL);
                                            writer.Write(-19);
                                            writer.Write(0);
                                            break;
                                        }
                                        FileStream f = files[handle];

                                        byte[] buffer = new byte[size * count];
                                        int sz = f.Read(buffer, 0, buffer.Length);
                                        writer.Write(BYTE_SPECIAL);
                                        writer.Write(sz / size);
                                        writer.Write(sz);
                                        writer.Write(buffer, 0, sz);
                                        if (reader.ReadByte() != BYTE_OK)
                                            throw new InvalidDataException();
                                    }
                                    else
                                    {
                                        writer.Write(BYTE_NORMAL);
                                    }
                                    break;
                                }
                            case BYTE_CLOSE:
                                {
                                    int fd = reader.ReadInt32();
                                    if ((fd & 0x0fff00ff) == 0x0fff00ff)
                                    {
                                        int handle = (fd >> 8) & 0xff;
                                        if (files[handle] == null)
                                        {
                                            writer.Write(BYTE_SPECIAL);
                                            writer.Write(-38);
                                            break;
                                        }
                                        Log(log, name + " close(" + handle.ToString() + ")");
                                        FileStream f = files[handle];

                                        writer.Write(BYTE_SPECIAL);
                                        writer.Write(0);
                                        f.Close();
                                        files[handle] = null;
                                    }
                                    else
                                    {
                                        // Check if it is a file to dump
                                        foreach (var item in files_request)
                                        {
                                            if (item.Key == fd)
                                            {
                                                FileStream dump_file = item.Value;
                                                if (dump_file == null)
                                                    break;

                                                Log(log, name + " -> dump complete(\"" + Path.GetFileName(dump_file.Name) + "\")");

                                                // Close file and remove from request list
                                                dump_file.Close();
                                                files_request.Remove(fd);

                                                break;
                                            }
                                        }

                                        // Send response
                                        writer.Write(BYTE_NORMAL);
                                    }
                                    break;
                                }
                            case BYTE_SETPOS:
                                {
                                    int fd = reader.ReadInt32();
                                    int pos = reader.ReadInt32();
                                    if ((fd & 0x0fff00ff) == 0x0fff00ff)
                                    {
                                        int handle = (fd >> 8) & 0xff;
                                        if (files[handle] == null)
                                        {
                                            writer.Write(BYTE_SPECIAL);
                                            writer.Write(-38);
                                            break;
                                        }
                                        FileStream f = files[handle];

                                        f.Position = pos;
                                        writer.Write(BYTE_SPECIAL);
                                        writer.Write(0);
                                    }
                                    else
                                    {
                                        writer.Write(BYTE_NORMAL);
                                    }
                                    break;
                                }
                            case BYTE_STATFILE:
                                {
                                    int fd = reader.ReadInt32();
                                    if ((fd & 0x0fff00ff) == 0x0fff00ff)
                                    {
                                        int handle = (fd >> 8) & 0xff;
                                        if (files[handle] == null)
                                        {
                                            writer.Write(BYTE_SPECIAL);
                                            writer.Write(-38);
                                            writer.Write(0);
                                            break;
                                        }
                                        FileStream f = files[handle];

                                        FSStat stat = new FSStat();

                                        stat.flags = FSStatFlag.None;
                                        stat.permission = 0x400;
                                        stat.owner = ids[1];
                                        stat.group = 0x101e;
                                        stat.file_size = (uint)f.Length;

                                        writer.Write(BYTE_SPECIAL);
                                        writer.Write(0);
                                        writer.Write(Marshal.SizeOf(stat));
                                        writer.Write(stat);
                                    }
                                    else
                                    {
                                        writer.Write(BYTE_NORMAL);
                                    }
                                    break;
                                }
                            case BYTE_EOF:
                                {
                                    int fd = reader.ReadInt32();
                                    if ((fd & 0x0fff00ff) == 0x0fff00ff)
                                    {
                                        int handle = (fd >> 8) & 0xff;
                                        if (files[handle] == null)
                                        {
                                            writer.Write(BYTE_SPECIAL);
                                            writer.Write(-38);
                                            break;
                                        }
                                        FileStream f = files[handle];
                                        
                                        writer.Write(BYTE_SPECIAL);
                                        writer.Write(f.Position == f.Length ? -5 : 0);
                                    }
                                    else
                                    {
                                        writer.Write(BYTE_NORMAL);
                                    }
                                    break;
                                }
                            case BYTE_GETPOS:
                                {
                                    int fd = reader.ReadInt32();
                                    if ((fd & 0x0fff00ff) == 0x0fff00ff)
                                    {
                                        int handle = (fd >> 8) & 0xff;
                                        if (files[handle] == null)
                                        {
                                            writer.Write(BYTE_SPECIAL);
                                            writer.Write(-38);
                                            writer.Write(0);
                                            break;
                                        }
                                        FileStream f = files[handle];

                                        writer.Write(BYTE_SPECIAL);
                                        writer.Write(0);
                                        writer.Write((int)f.Position);
                                    }
                                    else
                                    {
                                        writer.Write(BYTE_NORMAL);
                                    }
                                    break;
                                }
                            case BYTE_PING:
                                {
                                    int val1 = reader.ReadInt32();
                                    int val2 = reader.ReadInt32();

                                    Log(log, name + " PING RECEIVED with values : " + val1.ToString() + " - " + val2.ToString());
                                    break;
                                }
                            default:
                                throw new InvalidDataException();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (log != null)
                    Log(log, name + " " + e.Message);
                else
                    Console.WriteLine(name + " " + e.Message);
            }
            finally
            {
                foreach (var item in files)
                {
                    if (item != null)
                        item.Close();
                }
                foreach (var item in files_request)
                {
                    if (item.Value != null)
                        item.Value.Close();
                }

                if (log != null)
                    log.Close();
            }
            Console.WriteLine(name + " Exit");
        }
    }
}
