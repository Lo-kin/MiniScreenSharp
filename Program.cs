using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using OpenCvSharp;

namespace MiniScreenSharp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ScreenSerials screens = new ScreenSerials();
            screens.Run();
        }
    }

    public class FancyConsole
    {
        public Dictionary<string , (string , bool)> ValueStat = new();
        public Dictionary<string , Func<bool>> Menu = new();
        public bool ShowLock = false;
        public int SelectedIndex = 0;
        public int MenuIndex
        {
            get
            {
                return SelectedIndex;
            }
            set
            {
                if (value < 0)
                {
                    SelectedIndex = 0;
                }
                else if (value >= Menu.Count)
                {
                    SelectedIndex = Menu.Count - 1;
                }
                else
                {
                    SelectedIndex = value;
                }
            }
        }


        public bool SetValue(string key , string value)
        {
            if (ValueStat.ContainsKey(key) == false)
            {
                ValueStat.Add(key , (value , true));
            }
            else
            {
                if (ValueStat[key].Item1 != value)
                {
                    ValueStat[key] = (value , true);
                }
            }
            return true;
        }

        public bool ShowValue()
        {
            if (ShowLock == true)
            {
                return false;
            }
            Console.CursorLeft = 0;
            Console.CursorTop = 0;
            foreach (var item in ValueStat)
            {
                Console.Write(new string(" ".ToCharArray()[0], Console.BufferWidth));
                Console.CursorLeft = 0;
                Write($"{item.Key} : " , ConsoleColor.Cyan);
                if (item.Value.Item2 == true)
                {

                    WriteLine(item.Value.Item1 , ConsoleColor.Red);
                }
                else
                {
                    WriteLine(item.Value.Item1 , ConsoleColor.White);
                }
                ValueStat[item.Key] = (item.Value.Item1 , false);
            }
            return true;
        }

        public bool Selector()
        {
            while (true)
            {
                ShowLock = true;
                Console.CursorTop = ValueStat.Count + 1;
                Console.CursorLeft = 0;
                foreach (var item in Menu)
                {
                    Console.WriteLine(new string(" ".ToCharArray()[0], Console.BufferWidth));
                    Console.CursorLeft = 0;
                    if (Menu.ElementAt(MenuIndex).Key == item.Key)
                    {
                        WriteLine($"> {item.Key}" , ConsoleColor.Green);
                    }
                    else
                    {
                        WriteLine($"  {item.Key}" , ConsoleColor.White);
                    }
                }
                ShowLock = false;
                var readKey = Console.ReadKey(true);
                if (readKey.Key == ConsoleKey.UpArrow)
                {
                    MenuIndex--;
                }
                else if (readKey.Key == ConsoleKey.DownArrow)
                {
                    MenuIndex++;
                }
                else if (readKey.Key == ConsoleKey.Enter)
                {
                    var action = Menu.ElementAt(MenuIndex).Value;
                    if (action != null)
                    {
                        action();
                    }
                    Console.Clear();
                }
            }
        }

        public static void WriteLine(string text , ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
        public static void Write(string text, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }


    }

    public class ScreenSerials
    {
        public List<MiniScreen> Screens = [];

        public ScreenSerials()
        {
            Screens = GetScreens();
        }

        public bool Run()
        {
            if (Screens.Count == 0)
            {
                Console.WriteLine("No Screen Found!");
                return false;
            }
            while (true)
            {
                foreach (var item in Screens)
                {
                    try
                    {
                        item.Update();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                    Thread.Sleep(50);
                }
            }
        }

        static List<MiniScreen> GetScreens()
        {
            List<MiniScreen> list = [];

            foreach (var item in SerialPort.GetPortNames())
            {
                var device = new SerialPort(item, 19200);
                device.ReadTimeout = 100;
                byte[] bytes = new byte[6];
                try
                {
                    device.Open();
                    int count = device.Read(bytes, 0, bytes.Length);
                }
                catch (Exception)
                {
                    device.Close();
                    device.Dispose();
                    continue;
                }
                if (bytes.SequenceEqual(new byte[] { 0, 77, 83, 78, 48, 49 }))//串口数据\x00MSN01
                {
                    device.Write([ 0 ,77 ,83 ,78 , 67, 78 ] , 0 , 6);//握手\x00MSNCN
                    Thread.Sleep(150);
                    string GetRequest = device.ReadExisting();
                    if (GetRequest.StartsWith("\x00MSN") == true)//回应成功\x00MSNCN
                    {
                        MiniScreen screen = new MiniScreen() 
                        {
                            Device = device
                        };
                        screen.LoadImageSource("test.png");
                        list.Add(screen);
                    }
                    break;
                }
            }
            return list;
        }

        public bool CheckVaild(int _index)
        {
            if (_index >= 0 && _index < Screens.Count && Screens[_index] != null) 
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class MiniScreen
    {
        public SerialPort Device = new SerialPort();
        public FancyConsole ScreenInfo = new FancyConsole();
        public Size ScreenSize = new Size(160 , 80);
        public int Touch = 65536;
        public Stat TouchStat = new Stat();
        public List<byte[]> ImageData = [];
        public byte[] _data = [];
        public byte[] Data 
        {
            get { return _data; }
            set 
            { 
                _data = value;
                DataChanged = true;
            }
        }
        public bool DataChanged = true;

        public event Action UpdateEvent;

        public bool CheckVaild()
        {
            if (Device != null && Device.IsOpen)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public MiniScreen()
        {
            ScreenInfo.Menu.Add("发送图片", () => {
                Console.Write("输入路径: ");
                string path = Console.ReadLine();
                if (path == null)
                {
                    return false;
                }
                else
                {
                    return LoadImageSource(path);
                }
            });
            ScreenInfo.Menu.Add("清屏", () => {
                Erase(0,0);
                return true;
            });
            ScreenInfo.Menu.Add("写入文本", () => {
                Console.Write("输入文本: ");
                string text = Console.ReadLine();
                if (text == null)
                {
                    return false;
                }
                else
                {
                    return WriteText(text);
                }
            });
            ScreenInfo.Menu.Add("退出程序", () => {
                Dispose();
                Environment.Exit(0);
                return true;
            });
            Thread thread = new Thread(() => { ScreenInfo.Selector(); });
            thread.Start();
            UpdateEvent += () => {
                ScreenPressRead();
                ScreenInfo.SetValue("设备串口号", Device.PortName);
                ScreenInfo.SetValue("触摸参数", Touch.ToString());
                ScreenInfo.SetValue("触摸情况", TouchStat.NowStat.ToString());
                if (DataChanged)
                {
                    Send();
                    DataChanged = false;
                }
                if (TouchStat.NowStat == TwoStat.FreezeToActive)
                {
                    WriteText(">w<");
                }
                else if (TouchStat.NowStat == TwoStat.ActiveToFreeze)
                {
                    WriteText("\'w\'");
                }
                ScreenInfo.ShowValue();
            };
        }

        public bool Update()
        {
            UpdateEvent();
            return true;
        }

        public bool LoadImageSource(string path)
        {
            Mat img = Cv2.ImRead(path, ImreadModes.Color);
            if (img.Empty())
            {
                return false;
            }
            else
            {
                return LoadImage(img);
            }
        }

        public bool LoadImage(Mat OriginMat)
        {
            Data = [];
            PreAddImage(0 , 0 , ScreenSize.Width, ScreenSize.Height);

            Mat ResizeMat = OriginMat.Resize(ScreenSize);//rgb888 to rgb565 (6->4)
            List<uint> RGB565Data = [];
            for (int y = 0; y < ResizeMat.Height; y++)
            {
                for (int x = 0; x < ResizeMat.Width; x++)
                {
                    Vec3b color = ResizeMat.At<Vec3b>(y, x);
                    uint b = (uint)color[0] >> 3;
                    uint g = (uint)color[1] >> 2;
                    uint r = (uint)color[2] >> 3;

                    RGB565Data.Add((r << 11) |( g << 5 )| b);
                }
            }

            uint[] ImgData = [.. RGB565Data];

            List<byte> EmptyData = [];
            int BlockSize = 128;
            for (int i = 0; i < MathF.Floor(ImgData.Length / BlockSize); i ++)
            {
                uint[] blockData = [.. RGB565Data.GetRange(i * BlockSize , BlockSize)];
                uint[] blockCMD = new uint[(int)Math.Floor(BlockSize / 2d)];
                for (int j = 0; j < Math.Floor(BlockSize / 2d); j++)
                {
                    blockCMD[j] = blockData[j * 2 + 0] * 65536 + blockData[j * 2 + 1];
                }
                uint MaxResult = (from n in blockCMD
                          group n by n into g
                          orderby g.Count() descending
                          select g).First().First();//出现次数最多的值
                EmptyData.AddRange([2, 4]);
                EmptyData.AddRange(BitConverter.GetBytes(MaxResult).Reverse());//uint 转byte[]采用大端模式,下面同理
                for (int j = 0; j < Math.Floor(BlockSize / 2d); j++)
                {
                    if (blockData[j * 2 + 0] * 65536 + blockData[j * 2 + 1] != MaxResult)
                    {
                        EmptyData.AddRange([4, (byte)j]);
                        EmptyData.AddRange(BitConverter.GetBytes((ushort)blockData[j * 2 + 0]).Reverse());
                        EmptyData.AddRange(BitConverter.GetBytes((ushort)blockData[j * 2 + 1]).Reverse());
                    }
                }
                EmptyData.AddRange([2 , 3 , 8 , 1 , 0 , 0]);
            }
            Data = EmptyData.ToArray();
            RGB565Data.Clear();
            ResizeMat.Dispose();
            ImgData = [];
            EmptyData.Clear();
            return true;
        }


        public bool Send(byte[] _sendData)
        {
            if (CheckVaild() == true)
            {
                return false;
            }
            else
            {
                Device.Write(_sendData, 0, _sendData.Length);
            }
            return true;
        }

        public bool Send()
        {
            if (CheckVaild() == false)
            {
                return false;
            }
            else
            {
                Device.Write(Data, 0, Data.Length);
            }
            return true;
        }

        public bool ScreenPressRead()
        {
            if (CheckVaild() == false)
            {
                Touch = 0;
                return false;
            }
            else
            {
                try
                {
                    byte[] reqTouch = [8, 9, 0, 0, 0, 0];
                    Device.Write(reqTouch, 0, 6);
                    int re = Device.Read(reqTouch, 0, 6);
                    if (re < 6)
                    {
                        Touch = 0;
                        return false;
                    }
                    else
                    {
                        Touch = reqTouch[4] * 256 + reqTouch[5];
                        if (reqTouch[0] != 8 && reqTouch[1] != 9)
                        {
                            TouchStat.Update(TwoStat.Any);
                        }
                        else if (Touch < 3000)
                        {
                            TouchStat.Update(TwoStat.Active);
                        }
                        else
                        {
                            TouchStat.Update(TwoStat.Freeze);
                        }
                        return true;
                    }
                }
                catch
                {
                    Touch = 0;
                    TouchStat.Update(TwoStat.Any);
                    return false;
                }
            }
        }

        public bool WriteText(string text)
        {
            if (CheckVaild() == false)
            {
                return false;
            }
            for (int i = 0;i < text.Length; i++)
            { 
                WriteChar(text[i] , i*32 + (ScreenSize.Width / 2) - (text.Length * 32 / 2) , (ScreenSize.Height / 2) - (64 / 2));
            }
            return true;
        }

        public bool WriteChar(char text , int xPos , int yPos , int charWidth = 0 , int charHeight = 0)
        {
            if (CheckVaild() == false)
            {
                return false;
            }
            else
            {
                SetXY(xPos, yPos);
                SetColor(Color.WHITE, Color.BLACK);
                Device.Write([2, 3, 2, (byte)text, (byte)Math.Floor(3651d / 256), 3651 % 256] , 0 , 6);
                while (true)
                {
                    if (Device.ReadExisting().Length == 0)
                    {
                        break;
                    }
                }
                return true;
            }
        }

        public bool SetXY(int _x , int _y) => SetValue(mode: 0 , _x , _y);
        public bool SetSize(int _w , int _h) => SetValue(mode: 1 , _w , _h);
        public bool SetColor(Color _fColor, Color _bColor) => SetValue(mode: 2 , (int)_fColor, (int)_bColor);

        public bool SetValue(byte mode , int _x , int _y)
        {
            if (CheckVaild() == false)
            {
                return false;
            }
            else
            {
                Device.Write([2, mode, (byte)Math.Floor(_x / 256d) , (byte)(_x % 256) , (byte)Math.Floor(_y / 256d), (byte)(_y % 256)] , 0 , 6);
            }
            return true;
        }

        public bool PreAddImage(int _x , int _y , int _w , int _h)
        {
            if (CheckVaild() == false)
            {
                return false;
            }
            else
            {
                SetXY(_x, _y);
                SetSize(_w, _h);
                Device.Write([2, 3, 7, 0, 0, 0] , 0 , 6);
                return true;
            }
        }

        public bool Erase(int page , int size)
        {
            if (CheckVaild() == false)
            {
                return false;
            }
            else
            {
                Device.Write([3 ,2 ,0 ,0 ,0 ,0] , 0 , 6);
                return true;
            }
        }

        public bool Dispose()
        {
            if (Device != null && Device.IsOpen)
            {
                Device.Close();
                Device.Dispose();
                Device = null;
            }
            Data = [];
            return true;
        }
    }

    public struct Stat
    {
        public TwoStat NowStat = TwoStat.Any;

        public Stat()
        {

        }

        public bool IsChanged()
        {
            if (NowStat == TwoStat.ActiveToFreeze || NowStat == TwoStat.FreezeToActive)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool Update(TwoStat stat)
        {
            if (stat == TwoStat.Any)
            {
                if (NowStat == TwoStat.ActiveToFreeze)
                {
                    NowStat = TwoStat.Freeze;
                }
                else if (NowStat == TwoStat.FreezeToActive)
                {
                    NowStat = TwoStat.Active;
                }
                return true;
            }
            else if (NowStat == stat)
            {
                return false;
            }
            else if ((NowStat == TwoStat.Active || NowStat == TwoStat.FreezeToActive) && (stat == TwoStat.Freeze || stat == TwoStat.ActiveToFreeze))
            {
                NowStat = TwoStat.ActiveToFreeze;
                return true;
            }
            else if ((NowStat == TwoStat.Freeze || NowStat == TwoStat.ActiveToFreeze) && (stat == TwoStat.Active || stat == TwoStat.FreezeToActive))
            {
                NowStat = TwoStat.FreezeToActive;
                return true;
            }
            else//NowStat == Any
            {
                NowStat = stat;
                return true;
            }
            
            return true;
        }
    }

    public enum TwoStat
    {
        Active = 0,
        Freeze = 1,
        ActiveToFreeze = 2,
        FreezeToActive = 3,
        Any = 4
    }

    public enum Color
    {
        RED = 0xf800,
        GREEN = 0x07e0,
        BLUE = 0x001f,
        WHITE = 0xffff,
        BLACK = 0x0000,
        YELLOW = 0xFFE0,
        GRAY0 = 0xEF7D,
        GRAY1 = 0x8410,
        GRAY2 = 0x4208
    }
}
