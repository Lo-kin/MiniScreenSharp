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

            /*
            Console.WriteLine("Hello, World!");

            foreach (var item in SerialPort.GetPortNames())
            {
                Console.WriteLine($"{item}");
            }
            SerialPort s = new SerialPort("COM7", 19200);

            s.Open();
            while (true)
            {
                if (s.ReadExisting() == "\x00MSN01")
                {
                    Console.WriteLine("Complete");
                    break;
                }
                Thread.Sleep(150);
            }
            byte[] sh = System.Text.Encoding.UTF8.GetBytes("\x00MSNCN");
            foreach (var item in sh)
            {
                Console.Write($" {item} ");
            }
            s.Write(sh , 0 , sh.Length);
            Thread.Sleep(150);
            Console.WriteLine($"Shake : {s.ReadExisting()}");

            while (true)
            {
                byte[] reqTouch = [8, 9, 0, 0, 0, 0];
                s.Write(reqTouch, 0, 6);
                byte[] read = new byte[8];
                int re = s.Read(read, 0, 6);
                Console.WriteLine($"Request Touch  : {ByteToHex(read)}");
                //Thread.Sleep(350);
            }*/
        }

        private static string ByteToHex(byte[] Bytes)
        {
            string str = string.Empty;
            foreach (byte Byte in Bytes)
            {
                str += String.Format("{0:X2}", Byte) + " ";
            }
            return str.Trim();
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
            //Screens[0].LoadPicture(Cv2.ImRead("test.png", ImreadModes.Color));
            while (true)
            {
                foreach (var item in Screens)
                {
                    
                    item.ScreenPressRead();
                    if (item.DataChanged)
                    {
                        item.Send();
                        item.DataChanged = false;
                    }
                    if (item.TouchStat.NowStat == TwoStat.FreezeToActive)
                    {
                        item.WriteText((byte)(item.TouchStat.NowStat + 33));
                        Console.WriteLine($"Touched! {item.TouchStat.NowStat} ");
                        
                    }
                    else if (item.TouchStat.NowStat == TwoStat.ActiveToFreeze)
                    {
                        item.WriteText((byte)(item.TouchStat.NowStat + 33));
                        Console.WriteLine($"Released! {item.TouchStat.NowStat} ");
                    }
                    Console.WriteLine($"Touch : {item.Touch}");
                    //item.ScreenPressRead();//复位触摸状态
                }
                Thread.Sleep(50);
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
                        list.Add(new MiniScreen()
                        {
                            Device = device
                        });
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
        public SerialPort Device;
        public (int, int) Size = (160 , 80);
        public int Touch = 65536;
        public Stat TouchStat = new Stat();
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

        public bool LoadPicture(Mat d)
        {
            Data = [];
            List<byte> ImgData = d.Resize(new Size(Size.Item1, Size.Item2)).ToBytes().ToList();
            //for (int i = 0;
            //Data.AddRange([ 2, 3, 8, 1, 0, 0 ]);
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

        public int Read()
        {
            if (CheckVaild() == false)
            {
                return 0;
            }
            else
            {
                byte[] bytes = new byte[6];
                int count = 0;
                try
                {
                    count = Device.Read(bytes, 0, bytes.Length);
                    foreach (var item in bytes)
                    {
                        Console.Write($" {item} ");
                    }
                }
                catch
                {
                    
                }
                return count;


                //Console.WriteLine($"Request Touch : {bytes}");

            }
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
                    Touch = reqTouch[4]*256+ reqTouch[5];
                    if (Touch == 3651)
                    {
                        TouchStat.Update(TwoStat.Any);
                    }    
                    else if (Touch < 3300)
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
        }

        public bool WriteText(byte text)
        {
            if (CheckVaild() == false)
            {
                return false;
            }
            else
            {
                SetXY(0, 0);
                SetColor(Color.WHITE, Color.BLACK);
                Device.Write([2, 3, 2, text, (byte)Math.Floor(3651d / 256), 3651 % 256] , 0 , 6);
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

        //public bool 
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
