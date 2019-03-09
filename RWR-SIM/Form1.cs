using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

// 쓰레드 관련 함수
using System.Net;
using System.Net.Sockets;
using System.Threading;

// 사용자 클래스 추가
using BitField;
using Invoke.Commons;


namespace RWR_SIM
{

    enum REQ_MESSAGE 
    {
        // 운용 제어
        REQ_INIT = 0x01,

        //
        REQ_SYSTEMVARIABLE,

    }

    enum RES_MESSAGE
    {
        RES_INIT = 1,
        RES_SYSTEMVARIABLE
    }

    public struct STRHeader
    {
        public uint opCode;
        public uint uiDataLength;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct UNIHeader
    {
        [FieldOffset(0)]
        STRHeader stHeader;

        [FieldOffset(0)]
        public fixed byte cData[8];
    }

    public struct STRMessage
    {
        public uint opCode;
        public uint uiDataLength;
        //byte[] cData=new int[100];
    }

  
    public partial class Form1 : Form
    {
        bool m_bButtonStat=false;
        CServer m_theServer;
        uint m_nListRows = 1;

        public static Form1 _Form1;



        public Form1()
        {

            InitializeComponent();
            _Form1 = this;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            // 로그 목록창 업데이트
            // Set the view to show details.
            //listViewLog.View = View.Details;
            // Allow the user to edit item text.
            //listViewLog.LabelEdit = true;
            // Allow the user to rearrange columns.
            //listViewLog.AllowColumnReorder = true;
            // Display check boxes.
            //listViewLog.CheckBoxes = true;
            // Select the item and subitems when selection is made.
            //listViewLog.FullRowSelect = true;
            // Display grid lines.
            //listViewLog.GridLines = true;
            // Sort the items in the list in ascending order.
            //listViewLog.Sorting = SortOrder.Ascending;

            //listViewLog.Columns.Add("번호");
            //listViewLog.Columns.Add("날짜", 50 );
            //listViewLog.Columns.Add("명령어", 70 );
            //listViewLog.Columns.Add("내용", 500 );

            // 상태 바 업데이트
            DisplayStringInStatusBar();

            m_theServer = new CServer();

            Thread theThread = new Thread(new ThreadStart(m_theServer.ListenerThread));

            theThread.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (m_bButtonStat)
            {
                ButtonStart.BackColor = Color.Cyan;
                m_bButtonStat = false;
            }

            else
            {
                ButtonStart.BackColor = Color.White;
                m_bButtonStat = true;
            }
        }

        public void DisplayStringInStatusBar()
        {
            //
            toolStripStatusLabel1.Text = String.Format( "연결 대기" );

        }

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            
            m_theServer.Close();
        }

        public void LogMessagePrint( uint uiCommandCode)
        {
            DateTime dt;
            string strCommandCode;
            ListViewItem newItem = new ListViewItem();


            //listViewLog.BeginUpdate();
            newItem.Text = m_nListRows.ToString();
            //newItem.SubItems.Add( m_nListRows.ToString() );

            dt = DateTime.Now;
            newItem.SubItems.Add(dt.ToString("HH:mm:ss.ff"));
            switch ( uiCommandCode )
            {
                case (int)REQ_MESSAGE.REQ_INIT:
                    //strCommandCode = string.Format( "REQ_INIT:0x{X4}", uiCommandCode );
                    strCommandCode = string.Format("REQ_INIT:");
                    newItem.SubItems.Add( strCommandCode );
                    break;

                default:
                    break;
            }

            listViewLog.Items.Add(newItem);
            //listViewLog.EndUpdate();

            ++m_nListRows;

        }

    }


    public class CServer
    {
        const int iTcpipPort = 1024;

        public bool m_theListenerThread = true;

        private TcpListener m_theClient;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public void ListenerThread()
        {
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");

            m_theClient = new TcpListener(localAddr, iTcpipPort);
            m_theClient.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            m_theClient.Start();

            while (m_theListenerThread)
            {
                // _Form1.DisplayStringInStatusBar();
                Console.WriteLine("AAA");

                // 클라이언트의 연결 요청 확인
                while (!m_theClient.Pending())
                {
                    Thread.Sleep(100);
                    if( m_theListenerThread == false )
                    {
                        return;
                    }
                }

                // 클라이언트와의 통신처리 스레드 시작
                ConnectionHandler newConnection = new ConnectionHandler();
                newConnection.m_theThreadListener = this.m_theClient;
                Thread newThread = new Thread(new ThreadStart(newConnection.ClientHandler));
                newThread.Start();

            }
        }

        public void Close()
        {
            m_theListenerThread = false;
        }

    }

    [BitFieldNumberOfBitsAttribute(8)]
    struct ExampleBitField : IBitField
    {
        [BitFieldInfo(0, 1)]
        public bool Bit1 { get; set; }
        [BitFieldInfo(1, 1)]
        public byte Bit2 { get; set; }
        [BitFieldInfo(2, 2)]
        public byte TwoBits { get; set; }
        [BitFieldInfo(4, 4)]
        public byte FourBits { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct DataPacket
    {
        [MarshalAs(UnmanagedType.I1)]
        public byte bySource;

        [MarshalAs(UnmanagedType.I1)]
        public byte byDestnation;

        [MarshalAs(UnmanagedType.I2)]
        public ushort usCommandCode;

        [MarshalAs(UnmanagedType.I4)]
        public uint uiDataLength;

        //[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        //public string Subject;

        // Calling this method will return a byte array with the contents
        // of the struct ready to be sent via the tcp socket.
        public byte[] Serialize()
        {
            // allocate a byte array for the struct data
            var buffer = new byte[Marshal.SizeOf(typeof(DataPacket))];

            // Allocate a GCHandle and get the array pointer
            var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var pBuffer = gch.AddrOfPinnedObject();

            // copy data from struct to array and unpin the gc pointer
            Marshal.StructureToPtr(this, pBuffer, false);
            gch.Free();

            return buffer;
        }

        // this method will deserialize a byte array into the struct.
        public void Deserialize(ref byte[] data)
        {
            var gch = GCHandle.Alloc(data, GCHandleType.Pinned);
            this = (DataPacket)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(DataPacket));
            gch.Free();
        }
    }

    public class ConnectionHandler
    {
        public TcpListener m_theThreadListener;
        private DataPacket m_theRecvPacket;
        private DataPacket m_theSendvPacket;
        private NetworkStream m_theNS;
        //public DataPacket m_thePacket = new DataPacket();

        public ConnectionHandler()
        {
            m_theRecvPacket = new DataPacket();
            m_theSendvPacket = new DataPacket();
        }

        public void ClientHandler()
        {
            TcpClient theClient = m_theThreadListener.AcceptTcpClient();
            m_theNS = theClient.GetStream();

            byte[] cData = new byte[20];

            while (true)
            {

                try
                {
                    int ret = m_theNS.Read(cData, 0, Marshal.SizeOf(typeof(DataPacket)) );

                    m_theRecvPacket.Deserialize(ref cData);

                    ParsingPacket();

                    if (ret == 0)
                    {
                        return;
                    }
                    else
                    {
                        //uniHeaderMessage.cData = cData;
                    }

                }
                catch (Exception e)
                {
                    if (!theClient.Connected)
                    {
                        return;
                    }
                }
            }
            
            
        }

        ~ConnectionHandler()
        {

        }

        public void ParsingPacket()
        {
            byte[] buffer;


            Form1._Form1.LogMessagePrint(m_theRecvPacket.usCommandCode);

            m_theSendvPacket.bySource = 0;
            m_theSendvPacket.byDestnation = 0;
            switch (m_theRecvPacket.usCommandCode)
            {

                case (int) REQ_MESSAGE.REQ_INIT:
                    m_theSendvPacket.usCommandCode = (int) RES_MESSAGE.RES_INIT;
                    m_theSendvPacket.uiDataLength = 0;
                    break;

                case (int)REQ_MESSAGE.REQ_SYSTEMVARIABLE:
                    m_theSendvPacket.usCommandCode = (int)RES_MESSAGE.RES_SYSTEMVARIABLE;
                    m_theSendvPacket.uiDataLength = 0;
                    break;

                default :
                    break;
            }

            buffer = m_theSendvPacket.Serialize();
            m_theNS.Write(buffer, 0, Marshal.SizeOf(typeof(DataPacket)));

        }
    }






}
