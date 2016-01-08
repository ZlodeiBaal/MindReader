using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NeuroSky.ThinkGear;
using NeuroSky.ThinkGear.Algorithms;
using System.Threading;
using System.IO;
using System.IO.Ports;
using System.Collections;
using Accord;
using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Math;
using Accord.Audio;
using Accord.Statistics.Analysis;
using Accord.Statistics.Kernels;
using System.Net;
using System.Net.Sockets;
using Jayrock.Json;
using Jayrock.Json.Conversion;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.Structure;


namespace BrainAnalizer
{
   
    public partial class Form1 : Form
    {
        const int MemorySize = 8; //Размер памяти
        const int TimeToSave = 0; //Шаг по времени для записи
        const int VectorSize = 10; //Сколько параметров состояния мозга сохраняем
        SerialPort serialport;
        //Элемент состояния мозга для момента времени
        class BrainStat
        {
            public double Delta;
            public double Theta;
            public double A1;
            public double A2;
            public double B1;
            public double B2;
            public double G1;
            public double G2;
            public double Meditation;
            public double Attention;

            public BrainStat()
            {
                Delta = 0;
                Theta = 0;
                A1 = 0;
                A2 = 0;
                B1 = 0;
                B2 = 0;
                G1 = 0;
                G2 = 0;
                Meditation = 0;
                Attention = 0;
            }
            public BrainStat(BrainStat b)
            {
                for (int i = 0; i < 10; i++)
                    this.SetVal(i, b.GetVal(i));
            }
            public IEnumerator GetEnumerator()
            {
                yield return Delta;
                yield return Theta;
                yield return A1;
                yield return A2;
                yield return B1;
                yield return B2;
                yield return G1;
                yield return G2;
                yield return Meditation;
                yield return Attention;
            }
            public void SetVal(int val, double what)
            {
                switch (val)
                {
                    case 0:
                        Delta = what; break;
                    case 1: Theta = what; break;
                    case 2: A1 = what; break;
                    case 3: A2 = what; break;
                    case 4: B1 = what; break;
                    case 5: B2 = what; break;
                    case 6: G1 = what; break;
                    case 7: G2 = what; break;
                    case 8: Meditation = what; break;
                    case 9: Attention = what; break;
                }
            }
            public double GetVal(int val)
            {
                switch (val)
                {
                    case 0:
                        return Delta; break;
                    case 1: return  Theta; break;
                    case 2: return A1; break;
                    case 3: return A2; break;
                    case 4: return B1; break;
                    case 5: return B2; break;
                    case 6: return G1; break;
                    case 7: return G2; break;
                    case 8: return Meditation; break;
                    case 9: return Attention; break;
                    default: return 0;
                }
            }
            public BrainStat ICloneable()
            {
                BrainStat B = new BrainStat();
                for (int i = 0; i < 10; i++)
                    B.SetVal(i, this.GetVal(i));
                return B;
            }
        }
        //Менеджер состояния мозга во времени:
        //1) Ведёт запись состояний во времени
        //2) Усредняет память по времени
        //3) Реализует сохранение в файл
        class Mementor
        {
            //Память состояний мозга
            public List<BrainStat> Memory;
            BrainStat IntegratedState = new BrainStat();
            int TimeWithoutSave = 0; //Сколько времени назад сохранялись
            string FileToSave = ""; //Куда сохраняться
            bool InitSaving = false; //Нужно ли сохраняться
            //Инициализация
            public Mementor()
            {
                Memory = new List<BrainStat>();
            }
            public double[] Current()
            {
                double[] ans = new double[VectorSize];
            //    if (MemorySize == Memory.Count)
                for (int i = 0; i < VectorSize; i++)
                    ans[i] = IntegratedState.GetVal(i);
                return ans;
            }
            //Сохраняем в память текущее состояние, производим все операции требуемые
            public void AddToMemory(BrainStat B)
            {
                
               // Memory.Add(new BrainStat(B));
             //   if (Memory.Count > MemorySize)
                {
                 //   Memory.RemoveAt(0);
                   IntegratedState = new BrainStat(B);
                 //   CalculateMemory();
                    TryToSave();
                }

            }
            void CalculateMemory()
            {
                for (int i = 0; i < VectorSize; i++)
                {
                    List<double> TMParray = new List<double>();
                    for (int j = 0; j < MemorySize; j++)
                    {
                        TMParray.Add(Memory[j].GetVal(i));
                    }
                    TMParray.Sort();
                    IntegratedState.SetVal(i, TMParray[MemorySize / 2-1]);
                }
            }
            //Начинаем запись в файл
            public void StartSaving(string s)
            {
                InitSaving = true;
                FileToSave = s;
                if (File.Exists(FileToSave))
                    File.Delete(FileToSave);
            }
            //Оканчиваем запись в файл
            public void StopSaving()
            {
                InitSaving = false;
                TimeWithoutSave = 0;
            }
            //Пробуем сохранить в память
            void TryToSave()
            {
                if (InitSaving)
                {
                    if (TimeWithoutSave == TimeToSave)
                    {
                        TimeWithoutSave = 0;
                        for (int j = 0; j < VectorSize; j++)
                            File.AppendAllText(FileToSave, IntegratedState.GetVal(j).ToString() + " ");
                        File.AppendAllText(FileToSave,"\r\n");
                    }
                    else
                        TimeWithoutSave++;
                }
            }
        }
        static Mementor M = new Mementor();
        static BrainStat BS = new BrainStat();
        static Connector connector;
        class PosTime
        {
            public double Time;
            public double Pos;
            public PosTime(double p, double t)
            {
                Time = t;
                Pos = p;
        }
        }
        static List<PosTime> RAW_DATA = new List<PosTime>();
        public Form1()
        {
            InitializeComponent();

            TcpClient client;
            Stream stream;
            byte[] buffer = new byte[2048];
            int bytesRead;
            // Building command to enable JSON output from ThinkGear Connector (TGC)
	    //Сначала инициализируем подключение через серверного клиента
	    //Потом сразу сбросим его, чтобы подключиться через COM порт
            byte[] myWriteBuffer = Encoding.ASCII.GetBytes(@"{""enableRawOutput"": true, ""format"": ""Json""}");
     		  try
            {
                client = new TcpClient("127.0.0.1", 13854);
                stream = client.GetStream();

                // Sending configuration packet to TGC
                if (stream.CanWrite)
                {
                    stream.Write(myWriteBuffer, 0, myWriteBuffer.Length);
                }

                System.Threading.Thread.Sleep(5000);
                client.Close();
            }
            catch (SocketException se) { }

            System.Threading.Thread.Sleep(3000);
            connector = new Connector();
            connector.DeviceConnected += new EventHandler(OnDeviceConnected);
            connector.DeviceConnectFail += new EventHandler(OnDeviceFail);
            connector.DeviceValidating += new EventHandler(OnDeviceValidating);

            connector.ConnectScan("COM5");
        }
                static void OnDeviceConnected(object sender, EventArgs e) {

            Connector.DeviceEventArgs de = (Connector.DeviceEventArgs)e;

                    byte[] init = Encoding.Unicode.GetBytes("ready");
                    connector.Send("COM5", init);

                   
            de.Device.DataReceived += new EventHandler(OnDataReceived);

        }




        // Called when scanning fails

        static void OnDeviceFail(object sender, EventArgs e) {

       //     Console.WriteLine("No devices found! :(");

        }



        // Called when each port is being validated

        static void OnDeviceValidating(object sender, EventArgs e) {

        //    Console.WriteLine("Validating: ");

        }

        // Called when data is received from a device
        static DateTime dt_last;
        static TimeSpan ts;
        static int incomelength=0;
        static double[] data;
        static object forlock=new object();
        static void OnDataReceived(object sender, EventArgs e)
        {

            //Device d = (Device)sender;

            Device.DataEventArgs de = (Device.DataEventArgs)e;
            NeuroSky.ThinkGear.DataRow[] tempDataRowArray = de.DataRowArray;

            TGParser tgParser = new TGParser();
            tgParser.Read(de.DataRowArray);



            /* Loops through the newly parsed data of the connected headset*/
            // The comments below indicate and can be used to print out the different data outputs. 
            int SetThemAll = 0;
            List<PosTime> RAW = new List<PosTime>();
            for (int i = 0; i < tgParser.ParsedData.Length; i++)
            {

                if (tgParser.ParsedData[i].ContainsKey("Raw"))
                {

                    //   Console.WriteLine("Raw Value:" + tgParser.ParsedData[i]["Raw"]);
                    RAW.Add(new PosTime(tgParser.ParsedData[i]["Raw"],tgParser.ParsedData[i]["Time"]));

                }

                if (tgParser.ParsedData[i].ContainsKey("PoorSignal"))
                {

                    //The following line prints the Time associated with the parsed data
                    //Console.WriteLine("Time:" + tgParser.ParsedData[i]["Time"]);

                    //A Poor Signal value of 0 indicates that your headset is fitting properly
                    //              Console.WriteLine("Poor Signal:" + tgParser.ParsedData[i]["PoorSignal"]);

                  //  poorSig = (byte)tgParser.ParsedData[i]["PoorSignal"];
                }
                

                if (tgParser.ParsedData[i].ContainsKey("Attention"))
                {
                    SetThemAll++;
                    //                Console.WriteLine("Att Value:" + tgParser.ParsedData[i]["Attention"]);
                    BS.Attention = tgParser.ParsedData[i]["Attention"];
                }


                if (tgParser.ParsedData[i].ContainsKey("Meditation"))
                {
                    SetThemAll++;
                    //     Console.WriteLine("Med Value:" + tgParser.ParsedData[i]["Meditation"]);
                    BS.Meditation = tgParser.ParsedData[i]["Meditation"];
                }


                if (tgParser.ParsedData[i].ContainsKey("EegPowerDelta"))
                {
                    BS.Delta = tgParser.ParsedData[i]["EegPowerDelta"];
           //         Console.WriteLine("Delta: " + tgParser.ParsedData[i]["EegPowerDelta"]);
                    SetThemAll++;
                }
                if (tgParser.ParsedData[i].ContainsKey("EegPowerTheta"))
                {
                    BS.Theta = tgParser.ParsedData[i]["EegPowerTheta"];
            //        Console.WriteLine("Theta: " + tgParser.ParsedData[i]["EegPowerTheta"]);
                    SetThemAll++;
                }
                if (tgParser.ParsedData[i].ContainsKey("EegPowerAlpha1"))
                {
                    BS.A1 = tgParser.ParsedData[i]["EegPowerAlpha1"];
            //        Console.WriteLine("Alpha1: " + tgParser.ParsedData[i]["EegPowerAlpha1"]);
                    SetThemAll++;
                }
                if (tgParser.ParsedData[i].ContainsKey("EegPowerAlpha2"))
                {
                    BS.A2 = tgParser.ParsedData[i]["EegPowerAlpha2"];
             //       Console.WriteLine("Alpha2: " + tgParser.ParsedData[i]["EegPowerAlpha2"]);
                    SetThemAll++;
                }
                if (tgParser.ParsedData[i].ContainsKey("EegPowerBeta1"))
                {
                    BS.B1 = tgParser.ParsedData[i]["EegPowerBeta1"];
             //       Console.WriteLine("Beta1: " + tgParser.ParsedData[i]["EegPowerBeta1"]);
                    SetThemAll++;
                }
                if (tgParser.ParsedData[i].ContainsKey("EegPowerBeta2"))
                {
                    BS.B2 = tgParser.ParsedData[i]["EegPowerBeta2"];
              //      Console.WriteLine("Beta2: " + tgParser.ParsedData[i]["EegPowerBeta2"]);
                    SetThemAll++;
                }
                if (tgParser.ParsedData[i].ContainsKey("EegPowerGamma1"))
                {
                    BS.G1 = tgParser.ParsedData[i]["EegPowerGamma1"];
               //     Console.WriteLine("Gamma1: " + tgParser.ParsedData[i]["EegPowerGamma1"]);
                    SetThemAll++;
                }
                if (tgParser.ParsedData[i].ContainsKey("EegPowerGamma2"))
                {
                    BS.G2 = tgParser.ParsedData[i]["EegPowerGamma2"];
              //      Console.WriteLine("Gamma2: " + tgParser.ParsedData[i]["EegPowerGamma2"]);
                    SetThemAll++;
                }
                if (tgParser.ParsedData[i].ContainsKey("BlinkStrength"))
                {

                    //       Console.WriteLine("Eyeblink " + tgParser.ParsedData[i]["BlinkStrength"]);

                }
               

            }
            if (dt_last == null)
                dt_last = DateTime.Now;
            else
            {
                ts = DateTime.Now - dt_last;
                dt_last = DateTime.Now;
               
            }
            incomelength = RAW.Count;
            lock (forlock)
            {
                if (RAW.Count > 0)
                {
                    for (int j = 0; j < RAW.Count; j++)
                        RAW_DATA.Add(RAW[j]);
                    if (RAW_DATA.Count > 1000)
                    {
                        int k = RAW_DATA.Count;
                        for (int j = 0; j < k - 1000; j++)
                        {
                            RAW_DATA.RemoveAt(0);
                        }
                    }
                    //Signal s = new Signal()
                    ComplexSignal CS = new ComplexSignal(1, RAW_DATA.Count, 1);
                    if (RAW_DATA.Count >= 999)
                    {
                        data = new double[RAW_DATA.Count];
                        for (int i = 0; i < RAW_DATA.Count; i++)
                        {
                            data[i] = RAW_DATA[i].Pos;//10 * Math.Sin(Math.PI * (i / 15.0));//// +
                        }
                        Accord.Math.CosineTransform.DCT(data);
                    }
                }
            }
            if (SetThemAll == 10)
            {
                M.AddToMemory(BS);
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            updateList();
            textBox13.Text = Properties.Settings.Default.s1;
            textBox14.Text = Properties.Settings.Default.s2;
            textBox15.Text = Properties.Settings.Default.s3;
        }
        void updateList()
        {
            string[] DAdress = Directory.GetDirectories("Base");
            if (listBox1.Items.Count!=DAdress.Length)
            {
                listBox1.Items.Clear();
                foreach (string s in DAdress)
                {
                    listBox1.Items.Add(s);
                }
            }
        }
        Image<Bgr, double> GD = new Image<Bgr, double>(650, 160, new Bgr(0, 0, 0));
        int PosInPic = 0;
        private void timer1_Tick(object sender, EventArgs e)
        {
            textBox1.Text = BS.Delta.ToString();
            textBox2.Text = BS.Theta.ToString();
            textBox3.Text = BS.A1.ToString();
            textBox4.Text = BS.A2.ToString();
            textBox5.Text = BS.B1.ToString();
            textBox6.Text = BS.B2.ToString();
            textBox7.Text = BS.G1.ToString();
            textBox8.Text = BS.G2.ToString();
            textBox9.Text = BS.Meditation.ToString();
            textBox10.Text = BS.Attention.ToString();

            textBox11.Text = M.Memory.Count.ToString();

            textBox17.Text =  ts.TotalMilliseconds.ToString();
            textBox18.Text = incomelength.ToString();

           // pictureBox2.Refresh();
            lock (forlock)
            {
                if (RAW_DATA.Count >= 999)
                {
                    double newa = 0;
                    for (int i = 0; i < 160; i++)
                    {
                        GD.Data[i, PosInPic, 0] = (byte)Math.Min(Math.Abs( data[i + 1]), 500);//1000*Math.Abs(data[i + 1]);//
                        GD.Data[i, PosInPic, 1] = (byte)Math.Min(Math.Abs( data[i + 1]), 500);//1000 * Math.Abs(data[i + 1]);//
                        GD.Data[i, PosInPic, 2] = (byte)Math.Min(Math.Abs( data[i + 1]), 500);//1000 * Math.Abs(data[i + 1]);
                    }
                    for (int i = 16; i < 30; i++)
                    {
                        newa += Math.Abs(data[i + 1]);
                    }
                    textBox19.Text = newa.ToString();
                    int t = int.Parse(textBox3.Text)+int.Parse(textBox4.Text);
                  //  File.AppendAllText("tmp.txt", newa.ToString() + " " + t.ToString()+"\n\r");
                    PosInPic++;
                    if (PosInPic >= GD.Width)
                    {
                        PosInPic = 0;
                        GD.Save("tmp.bmp");
                    }
                }
            }

                pictureBox2.Image = GD.ToBitmap();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            connector.Close();
            Environment.Exit(0);
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            updateList();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex!=-1)
            {
                DateTime DT = DateTime.Now;
                string s = listBox1.Items[listBox1.SelectedIndex].ToString()+"\\"+DT.DayOfYear.ToString()+"-"+DT.Hour.ToString()+"-"+DT.Minute.ToString()+"-"+DT.Second.ToString()+".txt";
                M.StartSaving(s);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            M.StopSaving();
        }
        KernelSupportVectorMachine svm;
        private void button3_Click(object sender, EventArgs e)
        {

            string[] Second = File.ReadAllLines(textBox14.Text);

            string[] First = File.ReadAllLines(textBox13.Text);


            List<double[]> F = new List<double[]>();
            List<double[]> S = new List<double[]>();
            double Alpha1Thresh = int.MaxValue; //2000;//  int.MaxValue;//
            double Alpha2Thresh = int.MaxValue; //2000; //
            for (int i=0;i<First.Length;i++)
            {
                string[] s1 = First[i].Split(' ');
                if ((double.Parse(s1[2]) < Alpha1Thresh) && (double.Parse(s1[3]) < Alpha2Thresh))
                {
                    double[] ar = new double[VectorSize];
                    double sum = 0;
                    for (int j = 0; j < VectorSize; j++)
                    {
                        ar[j] = double.Parse(s1[j]);
                        if ((j < VectorSize - 2) && (2<=j ))
                        sum += ar[j];
                    }
                    for (int j = 2; j < VectorSize-2; j++)
                        ar[j] = ar[j] / 1000;
                    if (ar[0] > 2000)
                    {
                        ar[0] = 2000;
                    }
                    if (ar[1] > 2000)
                    {
                        ar[1] = 2000;
                    }
                    ar[0] = ar[0] / 2000;
                    ar[1] = ar[1] / 2000;
                    ar[VectorSize - 2] = ar[VectorSize - 2] / 100;
                    ar[VectorSize - 1] = ar[VectorSize - 1] / 100;
                    F.Add(ar);
                }
            }
            for (int i = 0; i < Second.Length; i++)
            {
                string[] s1 = Second[i].Split(' ');
                if ((double.Parse(s1[2]) < Alpha1Thresh) && (double.Parse(s1[3]) < Alpha2Thresh))
                {
                    double[] ar = new double[VectorSize];
                    double sum = 0;
                    for (int j = 0; j < VectorSize; j++)
                    {
                        ar[j] = double.Parse(s1[j]);
                        if ((j < VectorSize - 2) && (2 <= j))
                            sum += ar[j];
                    }
                    for (int j = 2; j < VectorSize - 2; j++)
                        ar[j] = ar[j] / 1000;
                    if (ar[0]>2000)
                    {
                        ar[0] = 2000;
                    }
                    if (ar[1] > 2000)
                    {
                        ar[1] = 2000;
                    }
                    ar[0] = ar[0] / 2000;
                    ar[1] = ar[1] / 2000;
                    ar[VectorSize - 2] = ar[VectorSize - 2] / 100;
                    ar[VectorSize - 1] = ar[VectorSize - 1] / 100;
                    S.Add(ar);
                }
            }
            int min = Math.Min(F.Count, S.Count);
            double[][] inputs = new double[2*min][];
            int[] outputs = new int[2*min];

            int VS = VectorSize; //ТУТ

            for (int j=0;j<min;j++)
            {
                inputs[j] = new double[VS];
                inputs[j + min] = new double[VS];
                for (int i = 0; i < VS; i++)
              //  for (int i = VectorSize - 2; i < VectorSize; i++)//ТУТ
                {
                    inputs[j][i] = F[j][i];//ТУТ
                    inputs[j + min][i] = S[j][i];//ТУТ
               //     inputs[j][i - VectorSize + 2] = F[j][i];//ТУТ
                 //   inputs[j + min][i - VectorSize + 2] = S[j][i];//ТУТ
                }
                outputs[j] = -1;
                outputs[j + min] = 1;
            }

            // Get only the output labels (last column)
            


            // Create the specified Kernel
            IKernel kernel = new Gaussian((double)0.560);
         //   IKernel kernel = new Polynomial(5, 500.0);

            // Creates the Support Vector Machine for 2 input variables
            svm = new KernelSupportVectorMachine(kernel, inputs: VS);

            // Creates a new instance of the SMO learning algorithm
            var smo = new SequentialMinimalOptimization(svm, inputs, outputs)
            {
                // Set learning parameters
                Complexity = (double)1.50,
                Tolerance = (double)0.001,
                PositiveWeight = (double)1.00,
                NegativeWeight = (double)1.00,
            };


            try
            {
                // Run
                double error = smo.Run();

            }
            catch (ConvergenceException)
            {
                
            }
         //   double d = svm.Compute(inputs[10]);
            points.Clear();
            Points = 0;
            points_mid.Clear();
            timer3.Enabled = true;
        }
        double ldm = 1500;
        double ldtm = 1500;
        private List<PointF> points = new List<PointF>();
        private List<PointF> points_mid = new List<PointF>();
        int Points = 0;
        private void timer3_Tick(object sender, EventArgs e)
        {
            double[] dt = M.Current();
            double sum = 0;
            for (int j = 0; j < VectorSize-2; j++)
            {
                sum += dt[j];
            }
            for (int i = 2; i < VectorSize - 2; i++)
                dt[i] = dt[i] / 1000;

            if (dt[0] > 2000)
            {
                dt[0] = 2000;
            }
            if (dt[1] > 2000)
            {
                dt[1] = 2000;
            }
            dt[0] = dt[0] / 2000;
            dt[1] = dt[1] / 2000;
            dt[VectorSize - 2] = dt[VectorSize - 2] / 100;
            dt[VectorSize - 1] = dt[VectorSize - 1] / 100;
            double d = svm.Compute(dt);
            textBox12.Text = d.ToString();
            if (Math.Abs(d*15) < 60)
            if (Points < pictureBox1.Width-1)
            {
                points.Add(new PointF(Points, 60 + (int)(15 * d)));
                Points++;
            }
            else
            {
                points.RemoveAt(0);
                for (int i=0;i<points.Count;i++)
                {
                    points[i] = new PointF(points[i].X-1,points[i].Y);
                }
                points.Add(new PointF(Points, 60 + (int)(15 * d)));
            }
           // File.AppendAllText("Res.txt", d.ToString()+"\r\n");
            pictureBox1.Refresh();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string s = "Res.txt";
            timer3.Enabled = false;
            points.Clear();
            points_mid.Clear();
            Points = 0;
            if (File.Exists(s))
                File.Delete(s);
     
            string[] First = File.ReadAllLines(textBox15.Text);////"Base\\Gitar\\350-1-34-9.txt");
       
            int min = First.Length;


            double[][] inputs = new double[min][];
            int[] outputs = new int[min];
            double DeltaThresh =int.MaxValue; // 2000;//  int.MaxValue;//
            double ThetaThresh = int.MaxValue; //2000;//

            for (int j = 0; j < min; j++)
            {
                inputs[j] = new double[VectorSize];
              //  inputs[j + min] = new double[VectorSize];
                string[] s1 = First[j].Split(' ');
                if ((double.Parse(s1[0]) < DeltaThresh) && (double.Parse(s1[1]) < ThetaThresh))
                {
                    double sum = 0;
                    for (int i = 0; i < VectorSize; i++)
                    {
                        inputs[j][i] = double.Parse(s1[i]);
                        if ((i<VectorSize-2)&&(i>=2))
                        {
                            sum += inputs[j][i];
                        }
                        
                    }
                    for (int i = 2; i < VectorSize - 2; i++)
                        inputs[j][i] = inputs[j][i] / 1000;

                    if (inputs[j][0] > 2000)
                    {
                        inputs[j][0] = 2000;
                    }
                    if (inputs[j][1] > 2000)
                    {
                        inputs[j][1] = 2000;
                    }
                    inputs[j][0] = inputs[j][0] / 2000;
                    inputs[j][1] = inputs[j][1] / 2000;
                    inputs[j][VectorSize - 2] = inputs[j][VectorSize - 2] / 100;
                    inputs[j][VectorSize - 1] = inputs[j][VectorSize - 1] / 100;

                    double[] inputs2 = new double[2];//ТУТ
                    inputs2[0] = inputs[j][VectorSize - 2];//ТУТ
                    inputs2[1] = inputs[j][VectorSize - 1];//ТУТ
                    outputs[j] = -1;

                    double d = svm.Compute(inputs[j]);//(inputs[j]);//ТУТ
                    if (points.Count<pictureBox1.Width-1)
                        if (Math.Abs(d*15)<60)
                    {
                        points.Add(new PointF((float)Points, (float)(60+15 * d)));
                        Points++;
                        int counter = 0;
                        PointF summ = new PointF(0, 0);
                        for (int k = points.Count - 1; k > Math.Max(0, points.Count - 30); k--)
                        {
                            counter++;
                            //  sum.X += points[j].X;
                            summ.Y += points[k].Y;
                        }
                        summ.X = points[points.Count - 1].X;//sum.X / counter;
                        if (counter != 0)
                        {
                            summ.Y = summ.Y / counter;
                            points_mid.Add(summ);
                        }
                    }
                    File.AppendAllText(s, d.ToString() + "\r\n");
                }
            }
            pictureBox1.Refresh();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1)
            {
                string[] DAdress = Directory.GetFiles(listBox1.Items[listBox1.SelectedIndex].ToString() + "\\");
                listBox2.Items.Clear();
                for (int i = 0; i < DAdress.Length; i++)
                    listBox2.Items.Add(DAdress[i]);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (listBox2.SelectedIndex!=-1)
            {
                textBox13.Text = listBox2.Items[listBox2.SelectedIndex].ToString();
                Properties.Settings.Default.s1 = textBox13.Text;
                Properties.Settings.Default.Save();
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (listBox2.SelectedIndex != -1)
            {
                textBox14.Text = listBox2.Items[listBox2.SelectedIndex].ToString();
                Properties.Settings.Default.s2 = textBox14.Text;
                Properties.Settings.Default.Save();
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (listBox2.SelectedIndex != -1)
            {
                textBox15.Text = listBox2.Items[listBox2.SelectedIndex].ToString();
                Properties.Settings.Default.s3 = textBox15.Text;
                Properties.Settings.Default.Save();
            }
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            if (points.Count > 1)
            {
                //  foreach (var point in points)
                //      g.DrawRectangle(Pens.Black, point.X - 2.0f, point.Y - 2.0f, 4.0f, 4.0f);
                int counter = 0;
                PointF sum = new PointF(0, 0);
                for (int j = points.Count - 1; j > Math.Max(0, points.Count - 30); j--)
                {
                    counter++;
                    //  sum.X += points[j].X;
                    sum.Y += points[j].Y;
                }
                sum.X = points[points.Count - 1].X;//sum.X / counter;
                sum.Y = sum.Y / counter;
                points_mid.Add(sum);
                try
                {
                    if (points.Count > 1)
                        g.DrawLines(Pens.Black, points.ToArray());
                    if (points_mid.Count > 1)
                        g.DrawLines(Pens.Red, points_mid.ToArray());
                }catch
                {

                }
            }
            g.DrawLine(Pens.Black, new Point(0, 60), new Point(pictureBox1.Width, 60));

        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox2.SelectedIndex!=-1)
            {
                FileInfo FI = new FileInfo(listBox2.Items[listBox2.SelectedIndex].ToString());
                textBox16.Text = FI.Length.ToString();
            }
        }
        int pos = 0;
        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
           

        }
    }
}
