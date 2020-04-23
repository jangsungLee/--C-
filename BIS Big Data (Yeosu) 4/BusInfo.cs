#define NoSort

using BIS_Big_Data__Yeosu__4.GetXMLClass.Attributes;
using BIS_Big_Data__Yeosu__4.GetXMLClass.Attributes.BusOperationInfo_ClassMembers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace BIS_Big_Data__Yeosu__4
{
    /// <summary>
    /// "정류장 버스 도착 정보 조회"와 "버스운행정보 조회 "가 승인되어 있어야함.
    /// (신청후 곧바로 승인표시가 뜨기는 하지만, 서버에서 "IP등록" 그리고 "ACCESS DENY해제"가 완벽하게 되기전까지 15분~30분 정도 소요되기때문에,
    /// 신청후에 직전에는 어느정도 기다린 후에 해야함)
    /// </summary>
    class BusInfo
    
    {
        // "C:\\Users\\Administrator\\Documents\\"  ==> ".\\"


        private string _ServiceKey = ""; // 서버용 인증키(UTF-8)

        // 아래의 클래스가 객체여야하는 이유 : ServiceKey를 전달받아야 하기 때문이다.
        public _getArrInfoByStopID getArrInfoByStopID;
        public _BusOperationInfo busOperationInfo;

        private Time _startTime = new Time(4, 50), _endTime = new Time(23, 30);
        private int _total_available_Connection_count = 10000;

        public Time startTime
        {
            set
            {
                _startTime = value;
            }
            get
            {
                return _startTime;
            }
        }
        public Time endTime
        {
            set
            {
                if (value.Hour * 3600 + value.Minute * 60 + value.Second < _startTime.Hour * 3600 + _startTime.Minute * 60 + _startTime.Second)
                    throw new Exception("시간은 하루 단위 범위 내에 있어야하기 때문에, endTime은 startTime보다 이전 시간일 수 없습니다.");
                _endTime = value;
            }
            get
            {
                return _endTime;
            }
        }

        public int total_available_Connection_count
        {
            get
            {
                return _total_available_Connection_count;
            }

            set
            {
                if (value < 0)
                    throw new Exception("서버에 연결 가능한 최대회수를 0보다 작은 값으로 설정할 수 없습니다.");
                _total_available_Connection_count = value;
            }
        }


        /// <summary>
        /// 금일에 서버에 연결한 회수를 의미합니다.
        /// </summary>
        public int connection_count
        {
            get
            {
                return Connection_count;
            }

            set
            {
                if (value < 0)
                    throw new Exception("connection_count 0보다 작을 수 없습니다.");
                Connection_count = value;
            }
        }
        private string regSubKey_name = "BIS";
        private bool Do_deleteRegistryAll = false;

        private bool isTerminate = false;
        private void DayReset()
        {
            Time ResetStartTime = new Time(00, 00);
            int NowTime_Minutes, _startTime_Minutes;
            TimeSpan time;

            while (!isTerminate)
            {
                if (!((_startTime.Hour * 60 + _startTime.Minute <= DateTime.Now.Hour * 60 + DateTime.Now.Minute)
                        && (DateTime.Now.Hour * 60 + DateTime.Now.Minute <= _endTime.Hour * 60 + _endTime.Minute) && !connection_count_excess))
                {
                    using (Mutex m = new Mutex(false, "MutexName_DayReset"))
                    {
                        Connection_count = 0;
                        BusStop_Routes.Clear();
                        BusStopNames.Clear();

                        // 민감하지 않은 자료 : BusStop_Routes, BusStopNames
                        // 민감한 자료 : rstop
                        // 이유 : rstop은 MakeBusesArrivalHistory함수에서는 중요한 정보, rstop의 잘못된 초기화는 동시에 운행되고 있는 한 노선의 수에 영향을 미침
                        for (int i = 0; i < rstopList.Count; i++)
                            if (rstopList[i] == null)
                                rstopList.RemoveAt(i);
                            else
                                rstopList[i].Clear();
                    }

                    time = TimeSpan.FromSeconds((NowTime_Minutes = (DateTime.Now.Hour * 60 + DateTime.Now.Minute)) <= (_startTime_Minutes = (ResetStartTime.Hour * 60 + ResetStartTime.Minute)) ?
                (_startTime_Minutes - NowTime_Minutes) * 60 : (_startTime_Minutes + (1440 - NowTime_Minutes)) * 60
                + (24 * 60 - (ResetStartTime.Hour * 60 + ResetStartTime.Minute)) * 60); // 00H : 00M : 00S

                    Thread.Sleep(((time.Minutes + time.Hours * 60) * 60) * 1000); // wait until the time is at 24 hours.
                }

            }
        }


        private Task resetTask;
        public BusInfo(string Servicekey)
        {
            _ServiceKey = Servicekey;
            getArrInfoByStopID = new _getArrInfoByStopID(Servicekey);
            busOperationInfo = new _BusOperationInfo(Servicekey);

            resetTask = new Task(DayReset);
            resetTask.Start();


            if (IsAdministrator())
            {
                RegistryKey hKey = null; // 32 bit-Processor가 64-bit 레지스트리에 접근 하는 경우에 사용함.
                RegistryKey rk = null;
                // 서브키를 얻어온다. 없으면 null
                if (!Environment.Is64BitProcess && Environment.Is64BitOperatingSystem)
                {
                    hKey = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);
                    rk = hKey.OpenSubKey("SOFTWARE", false).OpenSubKey(regSubKey_name, false);
                }
                else
                {
                    rk = Registry.LocalMachine.OpenSubKey("Software", false).OpenSubKey(regSubKey_name, false);
                }


                if (rk != null)
                {
                    String[] regListStr = rk.GetValueNames();
                    String[] Decrypted_str;
                    String regStr;

                    foreach (string regValueName in regListStr)
                    {
                        regStr = rk.GetValue(regValueName) as string;
                        Decrypted_str = decryptAES256(regStr).Split(' ');

                        if (Decrypted_str[0] == _ServiceKey)
                        {
                            String[] Date = Decrypted_str[2].Split('-');
                            int Year = Convert.ToInt32(Date[0]), Month = Convert.ToInt32(Date[1]), Day = Convert.ToInt32(Date[2]);

                            if (DateTime.Now.Year == Year && DateTime.Now.Month == Month && DateTime.Now.Day == Day)
                            {
                                Connection_count = Convert.ToInt32(Decrypted_str[1]);
                            }
                            else
                            {
                                rk.DeleteValue(regValueName);
                            }

                            break;
                        }
                    }
                    if (hKey != null)
                        hKey.Close();
                    rk.Close();
                }
            }
        }

        // Public implementation of Dispose pattern callable by consumers.
        ~BusInfo()
        {
            isTerminate = true;
            resetTask.Wait();

            if (!Do_deleteRegistryAll && IsAdministrator())
            {
                String plainText = String.Format("{0} {1} {2}", _ServiceKey, Connection_count, String.Format("{0}-{1}-{2}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day));
                String EncodeString = encryptAES256(plainText);
                String[] regListStr;
                String[] Decrypted_str;
                String regStr;
                int temp_seq = 1;
                String temp_str;
                int Year, Month, Day, Date_sub;
                String[] Date;
                bool isThere = false;

                RegistryKey hKey = null; // 32 bit-Processor가 64-bit 레지스트리에 접근 하는 경우에 사용함.
                RegistryKey rk = null;
                // 서브키를 얻어온다. 없으면 null
                if (!Environment.Is64BitProcess && Environment.Is64BitOperatingSystem)
                {
                    hKey = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);
                    rk = hKey.OpenSubKey("SOFTWARE", true).OpenSubKey(regSubKey_name, true);
                    // 없으면 서브키를 만든다.
                    if (rk == null)
                    {
                        rk = hKey.OpenSubKey("SOFTWARE", true);
                        rk = rk.CreateSubKey(regSubKey_name, true);
                    }
                }
                else
                {
                    rk = Registry.LocalMachine.OpenSubKey("Software", true).OpenSubKey(regSubKey_name, true);
                    // 없으면 서브키를 만든다.
                    if (rk == null)
                    {
                        // 해당이름으로 서브키 생성
                        rk = Registry.LocalMachine.CreateSubKey("Software").CreateSubKey(regSubKey_name);
                    }
                }

                regListStr = rk.GetValueNames();


                // *********************************************** param 정렬 *******************************************************
                // 유효기간 이상지나면 삭제
                foreach (string regValueName in regListStr)
                {
                    regStr = rk.GetValue(regValueName) as string;
                    Decrypted_str = decryptAES256(regStr).Split(' ');
                    Date = Decrypted_str[2].Split('-');

                    Year = Convert.ToInt32(Date[0]);
                    Month = Convert.ToInt32(Date[1]);
                    Day = Convert.ToInt32(Date[2]);

                    Date_sub = (Year - DateTime.Now.Year > 0 ? Year - DateTime.Now.Year : 0) * 365
                        + (Month - DateTime.Now.Month > 0 ? Month - DateTime.Now.Month : 0) * 28
                        + (Day - DateTime.Now.Day > 0 ? Day - DateTime.Now.Day : 0);

                    if (Date_sub > 31)
                    {
                        rk.DeleteValue(regValueName);
                    }

                    if (Decrypted_str[0] == _ServiceKey)
                        isThere = true;
                }
                // Param이름 순서대로 정렬(1 3 4 9 -> 1 2 3 4).
                foreach (string regValueName in regListStr)
                {
                    if (/*Convert.ToInt32*/ConvertStrToNaturalNumber(regValueName) != temp_seq)
                    {
                        temp_str = rk.GetValue(regValueName) as string;
                        rk.DeleteValue(regValueName);
                        rk.SetValue(String.Format("param{0}", temp_seq++), regValueName);
                    }
                }
                // ****************************************************************************************************************

                if (!isThere)
                {
                    regListStr = rk.GetValueNames();

                    if (regListStr.Length < 1)
                    {
                        regListStr = new String[1];
                        regListStr[0] = "0";
                    }
                    temp_seq = ConvertStrToNaturalNumber(regListStr[regListStr.Length - 1]) + 1;

                    // MessageBox.Show("등록되었습니다."));

                    rk.SetValue(String.Format("param{0}", temp_seq), EncodeString);
                }

                if (hKey == null)
                    hKey.Close();
                rk.Close();
            }
            else
                EmptyRegistry();
        }

        public void EmptyRegistry()
        {
            // 서브키를 얻어온다. 없으면 null
            RegistryKey rk = Registry.LocalMachine.OpenSubKey("Software", true).OpenSubKey(regSubKey_name, true);

            if (rk != null)
            {
                string regSubkey = "Software\\" + regSubKey_name;
                // 서브키 삭제
                Registry.LocalMachine.DeleteSubKey(regSubkey);
            }

            rk.Close();
        }

        public void deleteRegistryAll()
        {
            EmptyRegistry();
            Do_deleteRegistryAll = true;
        }

        public void showRegistryAll(bool setDecryption = false)
        {
               // 서브키를 얻어온다. 없으면 null
               RegistryKey rk = Registry.LocalMachine.OpenSubKey("Software", true).OpenSubKey(regSubKey_name, true);

            if (rk != null)
            {
                String[] regListStr = rk.GetValueNames();

                foreach (string regValueName in regListStr)
                {
                    if (setDecryption)
                        Console.WriteLine("{0}   : {1}", regValueName, decryptAES256(rk.GetValue(regValueName) as string));
                    else
                        Console.WriteLine("{0}\t:\t{1}", regValueName, rk.GetValue(regValueName) as string);
                }
            }
        }

        // *************************************************************************************************************************************
        //                                                 보조 데이터 암호화 관련

        // 키
        private static readonly string KEY = "01234567890123456789012345678901";

        //128bit (16자리)
        private static readonly string KEY_128 = KEY.Substring(0, 128 / 8);

        //256bit (32자리)
        private static readonly string KEY_256 = KEY.Substring(0, 256 / 8);

        //AES 256 암호화.., CBC, PKCS7, 예외발생하면 null
        public static string encryptAES256(string plain)
        {
            try
            {
                //바이트로 변환 
                byte[] plainBytes = Encoding.UTF8.GetBytes(plain);

                //레인달 알고리듬
                RijndaelManaged rm = new RijndaelManaged();
                //자바에서 사용한 운용모드와 패딩방법 일치시킴(AES/CBC/PKCS5Padding)
                rm.Mode = CipherMode.CBC;
                rm.Padding = PaddingMode.PKCS7;
                rm.KeySize = 256;

                //메모리스트림 생성
                MemoryStream memoryStream = new MemoryStream();

                //key, iv값 정의
                ICryptoTransform encryptor = rm.CreateEncryptor(Encoding.UTF8.GetBytes(KEY_256), Encoding.UTF8.GetBytes(KEY_128));
                //크립토스트림을 키와 IV값으로 메모리스트림을 이용하여 생성
                CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);

                //크립트스트림에 바이트배열을 쓰고 플러시..
                cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                cryptoStream.FlushFinalBlock();

                //메모리스트림에 담겨있는 암호화된 바이트배열을 담음
                byte[] encryptBytes = memoryStream.ToArray();

                //베이스64로 변환
                string encryptString = Convert.ToBase64String(encryptBytes);

                //스트림 닫기.
                cryptoStream.Close();
                memoryStream.Close();

                return encryptString;
            }
            catch (Exception)
            {
                return null;
            }
        }

        //AES256 복호화.., CBC, PKCS7, 예외발생하면 null
        public static string decryptAES256(string encrypt)
        {
            try
            {
                //base64를 바이트로 변환 
                byte[] encryptBytes = Convert.FromBase64String(encrypt);
                //byte[] encryptBytes = Encoding.UTF8.GetBytes(encryptString);

                //레인달 알고리듬
                RijndaelManaged rm = new RijndaelManaged();
                //자바에서 사용한 운용모드와 패딩방법 일치시킴(AES/CBC/PKCS5Padding)
                rm.Mode = CipherMode.CBC;
                rm.Padding = PaddingMode.PKCS7;
                rm.KeySize = 256;

                //메모리스트림 생성
                MemoryStream memoryStream = new MemoryStream(encryptBytes);

                //key, iv값 정의
                ICryptoTransform decryptor = rm.CreateDecryptor(Encoding.UTF8.GetBytes(KEY_256), Encoding.UTF8.GetBytes(KEY_128));
                //크립토스트림을 키와 IV값으로 메모리스트림을 이용하여 생성
                CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);

                //복호화된 데이터를 담을 바이트 배열을 선언한다. 
                byte[] plainBytes = new byte[encryptBytes.Length];

                int plainCount = cryptoStream.Read(plainBytes, 0, plainBytes.Length);

                //복호화된 바이트 배열을 string으로 변환
                string plainString = Encoding.UTF8.GetString(plainBytes, 0, plainCount);

                //스트림 닫기.
                cryptoStream.Close();
                memoryStream.Close();

                return plainString;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // *************************************************************************************************************************************
        //                                                 XML을 직접 받아와서 작업을 하는 함수들

        /// <summary>
        /// 모든 시내버스 노선들을 조회하여 List<KeyValuePair<string, string>>형태로 반환합니다.
        /// </summary>
        /// <param name="isWriteCSV">CSV파일을 쓸 것인지를 결정합니다.</param>
        /// <param name="isWriteXML">XML파일을 쓸 것인지를 결정합니다.</param>
        /// <param name="csvFileName">CSV 파일이름을 정합니다.</param>
        /// <param name="xmlFileName">XML 파일이름을 정합니다.</param>
        /// <returns>
        /// Key   : Route ID
        /// Value : Route Name
        /// 
        /// 내부적으로 Error가 발생하는 경우는 "List<KeyValuePair<string, string>>"가 "new List<KeyValuePair<string, string>>()"를 반환합니다.
        /// 즉, 빈 인스턴스를 반환합니다.
        /// 
        /// 반환값 사용예시:
        /// Dictionary<string, string> BusNames = GetBusNames();
        /// 
        /// foreach (KeyValuePair<string, string> kv in BusNames)
        /// {
        ///    Console.WriteLine("Bus Name: {0},\tBus ID: {1}", kv.Value, kv.Key);
        /// }
        /// </returns>
        public Dictionary<string, string> GetBusNames(bool isWriteCSV = false, bool isWriteXML = false, string csvFileName = @"Bus Information (Yeosu)", string xmlFileName = @"Bus Information (Yeosu)")
        {
            Dictionary<string, string> BusNames = new Dictionary<string, string>();

            string xml = GetXML(busOperationInfo.getRouteInfoAll.url);
            if (string.IsNullOrEmpty(xml))
            {
                /*try
                {
                    throw new ArgumentNullException("Exception");
                }
                catch (ArgumentNullException e)
                {
                    Console.WriteLine("XML을 받아올 수 없습니다..\n");

                    return BusNames; // Empty
                }*/
                throw new Exception("XML을 받아올 수 없습니다..\n");
            }


            XmlNodeList xml_node = XmlParser_for_itemList(xml);

            if (!isWriteCSV)
                foreach (XmlNode xn in xml_node)
                {
                    BusNames.Add(ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_ID]),
                        ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_NAME]) + " " + ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_DIRECTION]));
                }
            else
            {
                csvFileName += ".csv";
                using (StreamWriter outputFile = new StreamWriter("C:\\Users\\Administrator\\Documents\\" + csvFileName, false/*no append*/, Encoding.UTF8))
                {
                    outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}"
                        , _getRouteInfoAll.msgBody.ROUTE_ID, _getRouteInfoAll.msgBody.ROUTE_NAME, _getRouteInfoAll.msgBody.ROUTE_DIRECTION
                        , _getRouteInfoAll.msgBody.ST_STOP_ID, _getRouteInfoAll.msgBody.ED_STOP_ID
                        , _getRouteInfoAll.msgBody.TURN_USEFLAG, _getRouteInfoAll.msgBody.TURN_ORD
                        , _getRouteInfoAll.msgBody.FST_TIME, _getRouteInfoAll.msgBody.LST_TIME);

                    foreach (XmlNode xn in xml_node)
                    {
                        BusNames.Add(ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_ID]),
                            ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_NAME]) + " " + ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_DIRECTION]));



                        outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                            ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_ID]), ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_NAME]), ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ROUTE_NAME])
                        , ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ST_STOP_ID]), ConvertNodeToString(xn[_getRouteInfoAll.msgBody.ED_STOP_ID])
                        , ConvertNodeToString(xn[_getRouteInfoAll.msgBody.TURN_USEFLAG]), ConvertNodeToString(xn[_getRouteInfoAll.msgBody.TURN_ORD])
                        , ConvertNodeToString(xn[_getRouteInfoAll.msgBody.FST_TIME]), ConvertNodeToString(xn[_getRouteInfoAll.msgBody.LST_TIME]));
                    }

                    outputFile.Close();
                }
            }


            if (isWriteXML)
            {
                xmlFileName += ".xml";

                using (StreamWriter outputFile = new StreamWriter("C:\\Users\\Administrator\\Documents\\" + xmlFileName, false/*no append*/, Encoding.UTF8))
                {
                    outputFile.Write(xml);
                    outputFile.Close();
                }
            }

            return BusNames;
        }

        /// <summary>
        /// 모든 시내버스의 경유 경로를 조회하여, 여수시의 모든 정류장을 Dictionary Set형태로 반환합니다.
        /// </summary>
        /// <param name="BusNames">GetBusNames함수에서 반환값을 전달합니다.</param>
        /// <param name="isWriteCSVfile">csv파일을 쓸 것인지를 결정합니다.</param>
        /// <param name="isWriteXMLfile">xml파일을 쓸 것인지를 결정합니다.</param>
        /// <param name="csvDirPath">csv 폴더이름을 결정합니다.</param>
        /// <param name="xmlDirPath">xml 폴더이름을 결정합니다.</param>
        /// <returns>
        /// Key   : Bus Stop ID
        /// Value : Bus Stop Name
        /// 
        /// 내부적으로 Error가 발생하는 경우는 "Dictionary<string, string>"가 "new Dictionary<string, string>()"를 반환합니다.
        /// 즉, 빈 인스턴스를 반환합니다.
        /// 
        /// 반환값 사용예시:
        /// Dictionary<string, string> BusNames = GetBusNames();
        /// if(BusNames.Count < 1)
        /// {
        ///     Console.WriteLine("전체 버스노선을 받아올 수 없습니다.");
        /// }
        /// else
        /// {
        ///     Dictionary<string, string> BusStopNames = GetBusStopNames(BusNames);
        ///     Console.WriteLine("정류장 수 : " + BusStopNames.Count);
        ///     Console.WriteLine("\n");
        ///
        ///     foreach (KeyValuePair<string, string> kv in BusStopNames)
        ///     {
        ///         //Console.WriteLine("BusStop Name: {0},\tBusStop ID: {1}", kv.Value, kv.Key);
        ///         //Console.WriteLine("{0},{1}", kv.Value, kv.Key);
        ///     }
        /// }
        public Dictionary<string, string> GetBusStopNames(Dictionary<string, string> BusNames
            , bool isWriteCSVfile = false, bool isWriteXMLfile = false
            , string csvDirPath = @"BusStop (Yeosu) CSV folder", string xmlDirPath = @"BusStop (Yeosu) XML folder")
        {
            csvDirPath = "C:\\Users\\Administrator\\Documents\\" + csvDirPath;
            xmlDirPath = "C:\\Users\\Administrator\\Documents\\" + xmlDirPath;

            Dictionary<string, string> BusStopNames = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> kv in BusNames)
            {
                string xml = GetXML(busOperationInfo.getStaionByRoute.url_of_Base, kv.Key);
                if (string.IsNullOrEmpty(xml))
                {
                    try
                    {
                        throw new ArgumentNullException("Exception");
                    }
                    catch (ArgumentNullException e)
                    {
                        Console.WriteLine(kv.Key + "(Route ID) - " + kv.Value + "(Route Name) " + " XML을 받아올 수 없습니다..\n");

                        //return BusStopNames; // Empty
                        continue;
                    }
                    /*throw new Exception(kv.Key + "(Route ID) - " + kv.Value + "(Route Name) " + " XML을 받아올 수 없습니다..\n");*/
                }

                // Console.WriteLine("Key: {0}, Value: {1}", kv.Key, kv.Value);
                XmlNodeList xml_node = XmlParser_for_itemList(xml);

                foreach (XmlNode xn in xml_node)
                {
                    if (BusStopNames.ContainsKey(ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID])))
                    {
                        // Console.WriteLine("키가 이미 존재합니다.)
                    }
                    else
                    {
                        BusStopNames.Add(ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID]), ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_NAME]));

                    }
                }

                if (isWriteCSVfile)
                {
                    string csvFileName = csvDirPath + "\\" + kv.Value + " (Bus ID " + kv.Key + ").csv";
                    DirectoryInfo di = new DirectoryInfo(csvDirPath);
                    if (di.Exists == false)
                    {
                        di.Create();
                    }

                    using (StreamWriter outputFile = new StreamWriter(csvFileName, false/*no append*/, Encoding.UTF8))
                    {
                        // Console.WriteLine("*****************************************************************");
                        outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}"
                            , _getStaionByRoute.msgBody.STOP_ID, _getStaionByRoute.msgBody.STOP_NAME, _getStaionByRoute.msgBody.STOP_ORD
                            , _getStaionByRoute.msgBody.ROUTE_ID, _getStaionByRoute.msgBody.ROUTE_NAME
                            , _getStaionByRoute.msgBody.SECT_ACC_DISTANCE, _getStaionByRoute.msgBody.TOTAL_SECT_DISTANCE
                            , _getStaionByRoute.msgBody.SERVICE_ID
                            , _getStaionByRoute.msgBody.LAT, _getStaionByRoute.msgBody.LNG);

                        foreach (XmlNode xn in xml_node)
                        {
                            outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}"
                                , ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID]), ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_NAME]), ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ORD])
                                , ConvertNodeToString(xn[_getStaionByRoute.msgBody.ROUTE_ID]), ConvertNodeToString(xn[_getStaionByRoute.msgBody.ROUTE_NAME])
                                , ConvertNodeToString(xn[_getStaionByRoute.msgBody.SECT_ACC_DISTANCE]), ConvertNodeToString(xn[_getStaionByRoute.msgBody.TOTAL_SECT_DISTANCE])
                                , ConvertNodeToString(xn[_getStaionByRoute.msgBody.SERVICE_ID])
                                , ConvertNodeToString(xn[_getStaionByRoute.msgBody.LAT]), ConvertNodeToString(xn[_getStaionByRoute.msgBody.LNG]));
                        }

                        outputFile.Close();
                    }
                }
                if (isWriteXMLfile)
                {

                    string xmlFileName = xmlDirPath + "\\" + kv.Value + " (Bus ID " + kv.Key + ").xml";
                    DirectoryInfo di = new DirectoryInfo(xmlDirPath);
                    if (di.Exists == false)
                    {
                        di.Create();
                    }

                    using (StreamWriter outputFile = new StreamWriter(xmlFileName, false/*no append*/, Encoding.UTF8))
                    {
                        outputFile.Write(xml);
                        outputFile.Close();
                    }
                }
            }



            return BusStopNames;
        }

        /// <summary>
        /// 해당 ROUTE ID가 지나다니는 정류장들을 List<KeyValuePair<Int32, string>> 자료형으로 반환합니다.
        /// </summary>
        /// <param name="BusID">ROUTE ID</param>
        /// <returns>
        /// Key : Stop ID
        /// Value : Stop Name
        /// 
        /// 내부적으로 Error가 발생하는 경우는 "List<KeyValuePair<Int32, string>>"가 "new List<KeyValuePair<Int32, string>>()"으로 반환합니다.
        /// 즉, 빈 인스턴스를 반환합니다.
        /// </returns>
        public List<KeyValuePair<Int32, string>> GetBusStopes_moving(string Route_ID)

        {
            // Console.WriteLine("Key: {0}, Value: {1}", kv.Key, kv.Value);
            string xml = GetXML(busOperationInfo.getStaionByRoute.url_of_Base, Route_ID);
            List<KeyValuePair<Int32, string>> Stops = new List<KeyValuePair<Int32, string>>();
            string StopName;
            int StopID;

            if (string.IsNullOrEmpty(xml))
            {
                try
                {
                    throw new ArgumentNullException("Exception");
                }
                catch (ArgumentNullException e)
                {
                    Console.WriteLine(Route_ID + "(Route ID) XML을 받아올 수 없습니다..\n");

                    //return CommonBusStopNames; // Empty
                    return null;
                }
            }
            XmlNodeList xml_node = XmlParser_for_itemList(xml);


            foreach (XmlNode xn in xml_node) // 한 노선별 경유 정류장
            {
                StopID = ConvertStrToNaturalNumber(ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID]));
                StopName = ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_NAME]);

                Stops.Add(new KeyValuePair<Int32, string>(StopID,
                    StopName));
            }

            return Stops;
        }

        /// <summary>
        /// 각 정류장을 경유하는 시내버스 목록을 Dictionary Set형태로 반환합니다.
        /// Dictionary Set의 Value는 List로 되어있어, 시내버스들이 어느 정류장을 거치는지 알 수 있습니다.
        /// 
        /// 이 함수를 사용할 때 csv파일도 함께 쓸 때, GetBusStopNames함수도 같이 실행될 수 있습니다.
        /// 그 의미는 "BusStop (Yeosu).csv"파일도 새로 갱신된다는 것을 의미합니다.
        /// 
        /// 참고로 csv파일을 쓰는 것은 default인자를 사용하여 기본값으로 설정이 되어있습니다.
        /// 
        /// 이 함수는 정류장이 출발순서대로 출력된다는 것을 이용했습니다.
        /// (예를 들어 "시내버스 2번"은 "국동주택단지" => "봉산동게장골목"가 먼저 나오고, 다음에 "봉산동게장골목"이 나온다는 것을 이용했습니다.)
        /// </summary>
        /// <param name="BusNames">GetBusNames함수에서 반환한 값 "Dictionary Set"을 의미합니다.</param>
        /// <param name="BusStopNames">CSV파일을 쓸 때, 버스 정류장을 새로 조회하지 않고 GetBusStopNames함수에서 반환한 값 "Dictionary Set"을 사용합니다.</param>
        /// <param name="isWriteCSVfile">csv파일을 쓸것인지를 결정합니다.</param>
        /// <param name="csvFile_Name">출력할 csv파일이름을 결정합니다.</param>
        /// <returns>
        /// Key   : Bus Stop ID
        /// Value : List (해당 Key를 지나가는 버스의 Route ID와 RouteName)
        /// 
        /// Error발생시 반환값의 Count는 0이고, 정상적인경우 반환값의 Count는 0보다 큰 값을 가집니다.
        /// 반환값 사용예시
        /// foreach (KeyValuePair<string, List<KeyValuePair<string, string>>> kv in passingBusStop)
        /// {
        ///     string Stop_ID = kv.Key;
        ///     List<Dictionary<string, string>> PassingBus = kv.Value;
        /// }
        /// </returns>
        public Dictionary<string, List<KeyValuePair<string, string>>> GetPassingBy_BusStopNames_for_Buses(Dictionary<string, string> BusNames
            , Dictionary<string, string> BusStopNames = null
            , bool isWriteCSVfile = false, string csvFile_Name = @"BusStop Passing By (Yeosu).csv")
        {
            Dictionary<string, List<KeyValuePair<string, string>>> Dupplicated_BusStopNames = new Dictionary<string, List<KeyValuePair<string, string>>>();

            if (BusNames.Count < 1)
            {
                try
                {
                    throw new ArgumentNullException("Exception");
                }
                catch (ArgumentNullException e)
                {
                    Console.WriteLine("GetDuplicatedBusStopNames() - [BusNames.Count < 1]");

                    return Dupplicated_BusStopNames; // Empty
                }
            }

            foreach (KeyValuePair<string, string> kv in BusNames)
            {
                // Console.WriteLine("Key: {0}, Value: {1}", kv.Key, kv.Value);
                string xml = GetXML(busOperationInfo.getStaionByRoute.url_of_Base, kv.Key);
                if (string.IsNullOrEmpty(xml))
                {
                    try
                    {
                        throw new ArgumentNullException("Exception");
                    }
                    catch (ArgumentNullException e)
                    {
                        Console.WriteLine(kv.Key + "(Route ID) - " + kv.Value + "(Route Name) " + " XML을 받아올 수 없습니다..\n");

                        //return CommonBusStopNames; // Empty
                        continue;
                    }
                }
                XmlNodeList xml_node = XmlParser_for_itemList(xml);


                foreach (XmlNode xn in xml_node) // 한 노선별 경유 정류장
                {

                    if (Dupplicated_BusStopNames.ContainsKey(ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID])))
                    {
                        // Console.WriteLine("이미 등록되어 있습니다.");

                        KeyValuePair<string, string> OneBus = new KeyValuePair<string, string>(kv.Key, kv.Value);
                        List<KeyValuePair<string, string>> Buses;

                        Dupplicated_BusStopNames.TryGetValue(ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID]), out Buses);

                        Buses.Add(OneBus);
                        Dupplicated_BusStopNames.Remove(ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID]));
                        Dupplicated_BusStopNames.Add(ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID]), Buses);
                    }
                    else
                    {
                        // Console.WriteLine("등록되어 있지 않습니다.");

                        KeyValuePair<string, string> OneBus = new KeyValuePair<string, string>(kv.Key, kv.Value);
                        List<KeyValuePair<string, string>> Buses = new List<KeyValuePair<string, string>>();
                        Buses.Add(OneBus);

                        Dupplicated_BusStopNames.Add(ConvertNodeToString(xn[_getStaionByRoute.msgBody.STOP_ID]), Buses);
                    }
                }
            }

            if (isWriteCSVfile)
            {
                if (BusStopNames.Count < 1)
                    BusStopNames = GetBusStopNames(BusNames);

                using (StreamWriter outputFile = new StreamWriter("C:\\Users\\Administrator\\Documents\\" + csvFile_Name, false/*no append*/, Encoding.UTF8))
                {
                    //Console.WriteLine("*****************************************************************");
                    outputFile.WriteLine("BusStop_ID,BusStop_NAME,PassingBus_Count");

                    foreach (KeyValuePair<string, List<KeyValuePair<string, string>>> kv in Dupplicated_BusStopNames)
                    {
                        //Console.WriteLine("{0},{1},{2}", kv.Key, BusStopNames[kv.Key], kv.Value.Count);
                        outputFile.WriteLine("{0},{1},{2}", kv.Key, BusStopNames[kv.Key], kv.Value.Count);
                    }
                }
            }

            return Dupplicated_BusStopNames;
        }

        private static Dictionary<string, List<KeyValuePair<Int32, string>>> BusStop_Routes = new Dictionary<string, List<KeyValuePair<Int32, string>>>();
        private const int maxStopStep = 5;

        /// <summary>
        /// 해당 프로젝트의 주 기능(전달받은 XML을 분석하고, 만약 변경된 버스 도착정보가 있다면 업데이트하여 기록함)
        /// Connection횟수를 줄이기 위해 XML을 직접 받지 않고, 함수 호출시 XML을 전달받음.
        /// 
        /// 개발자측면(해당 함수 코드 수정용)
        /// 총 4개의 출력부분이 있음
        ///    1. 새로 운행이 추가된 노선(ROUTE ID) - (추가)
        ///    2. 기존에 추가된 노선(ROUTE ID)의 RSTOP이 변경되었을 경우 - (수정)
        ///    3. 기존에 존재하는 노선중에 새 회차의 노선(ROUTE ID) - (추가)
        ///    4. 해당 정류장에 도착한 노선 - (삭제)
        /// </summary>
        /// <param name="xml">해당 정류장의 도착정보가 담긴 XML(정류장 버스 도착 정보 조회 - 상세기능 : getArrInfoByStopID)</param>
        /// <param name="rstop">노선들을 기억하기 위한 Dictionary 자료형 메모리, 인스턴스를 할당해서 전달해주면 됨.</param>
        /// <param name="ROUTE_ID">전달한 XML에서의 ROUTE ID, CSV파일을 저장하는데 사용되며 Sort파일을 만들 때 중요한 역할을 함.</param>
        /// <returns>
        /// 업데이트 된 각 노선들의 회차 정보(노선추가, 이동, 회차추가, 도착)
        /// 
        /// 사용법:
        /// 
        ///     Dictionary<string, Dictionary<string, List<XmlNode>>> result = bis.MakeBusesArrivalHistory(xml1, ref rstop1, "구봉중학교앞 도착정보");
        ///     Dictionary<string, List<XmlNode>> Buses_Info;
        ///     String Attribute = "도착"
        /// 
        ///     if (result.ContainsKey(Attribute))
        ///     {
        ///         Buses_Info = result[Attribute];
        ///
        ///         Console.WriteLine("{0}한 노선들:", Attribute);
        ///         foreach(KeyValuePair<string, List<XmlNode>> OneBus_Info in Buses_Info)
        ///         {
        ///             Console.WriteLine("\tROUTE ID : {0}", OneBus_Info.Key);
        ///
        ///             foreach (XmlNode OneBusCycle_Info in OneBus_Info.Value)
        ///             {
        ///                 Console.WriteLine("\t\tROUTE NAME : {0}, RSTOP : {1}, STOP ID(Current Position) : {2}, STOP NAME(Current Position) : {3}"
        ///                    , OneBusCycle_Info[_getArrInfoByStopID.msgBody.ROUTE_NAME]
        ///                    , OneBusCycle_Info[_getArrInfoByStopID.msgBody.RSTOP]
        ///                    , OneBusCycle_Info[_getArrInfoByStopID.msgBody.STOP_ID]
        ///                    , OneBusCycle_Info[_getArrInfoByStopID.msgBody.STOP_NAME]); // ALLOC_TIME 같은 애트리뷰트도 "_getArrInfoByStopID.msgBody.ALLOC_TIME"와 같이 사용하면 됨.
        ///             }
        ///         }
        ///     }
        /// </returns>

        Dictionary<string, string> BusStopNames = new Dictionary<string, string>();
        private String csvDirPath;
        public Dictionary<string, Dictionary<string, List<XmlNode>>> MakeBusesArrivalHistory(string xml, ref Dictionary<string, List<XmlNode>> rstop, bool isWriteCSVFile=true)
        {
            XmlNodeList xml_node;
            Dictionary<string, List<XmlNode>> QueryRstop = new Dictionary<string, List<XmlNode>>();
            KeyValuePair<Int32, string> BusStop_keyValuePair;
            Dictionary<string, Dictionary<string, List<XmlNode>>> results = new Dictionary<string, Dictionary<string, List<XmlNode>>>();

            if (string.IsNullOrEmpty(xml))
            {
                try
                {
                    throw new ArgumentNullException("MakeBusesArrivalHistory함수 - XML내용이 비어있습니다.");
                }
                catch (ArgumentNullException e)
                {
                    return results;
                }
            }
            xml_node = XmlParser_for_itemList(xml);

            using (Mutex m = new Mutex(false, "MutexName2"))
            {
                if (!BusStopNames.ContainsKey(ConvertNodeToString(xml_node[0][_getArrInfoByStopID.msgBody.STOP_ID]))) // STOP_ID는 query한 STOP_ID와 같다는 것을 이용, 즉 모든 노드의 STOP_ID는 동일한 값을 가짐
                    BusStopNames.Add(ConvertNodeToString(xml_node[0][_getArrInfoByStopID.msgBody.STOP_ID]), ConvertNodeToString(xml_node[0][_getArrInfoByStopID.msgBody.STOP_NAME]));
            }

            String csvFileName = String.Format("{0} 도착정보", ConvertNodeToString(xml_node[0][_getArrInfoByStopID.msgBody.STOP_NAME]));
            String _csvFileName;

            ConsoleColor consoleColor;

            int memory_rstop;
            int count;
            int query_rstop;
            csvDirPath = "C:\\Users\\Administrator\\Documents\\"
                + String.Format("{0}\\{1}년 {2}월", "Bus Information for Big Data", DateTime.Now.Year, DateTime.Now.Month);
            csvFileName = String.Format("{0}\\{1} 도착정보 (STOP_ID - {2}, {3}-{4}-{5} {6}).csv"
                , csvDirPath
                , csvFileName
                , ConvertNodeToString(xml_node[0][_getArrInfoByStopID.msgBody.STOP_ID])
                , DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.DayOfWeek);
            _csvFileName = csvFileName;

            DirectoryInfo di = new DirectoryInfo(csvDirPath);
            if (di.Exists == false)
            {
                di.Create();
            }

#if DEBUG1234
            string xmlDirPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +"\\"
            + String.Format("XML {0} ({1}년 {2}월 {3}일)", csvFileName, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
            di = new DirectoryInfo(xmlDirPath);
            if (di.Exists == false)
            {
                di.Create();
            }
            String dir = String.Format("{0}\\MakeBusesArrivalHistory ({1}).xml", xmlDirPath, DateTime.Now.ToString().Replace(':', '#'));
            using (StreamWriter outputFile = new StreamWriter(String.Format("{0}\\History ({1}).xml", xmlDirPath, DateTime.Now.ToString().Replace(':', '#'))
                , false/*no append*/, Encoding.UTF8))
            {
                outputFile.Write(xml);
                outputFile.Close();
            }
#endif

            // csv Header
            {
                FileInfo fileInfo = new FileInfo(csvFileName);
                if (!fileInfo.Exists)
                {
                    if(isWriteCSVFile)
                    using (StreamWriter outputFile = new StreamWriter(csvFileName, false, Encoding.UTF8))
                    {
                        outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}"
                            , _getArrInfoByStopID.msgBody.ROUTE_ID //"ROUTE ID"
                            , _getArrInfoByStopID.msgBody.ROUTE_NAME //"ROUTE NAME"
                            , _getArrInfoByStopID.msgBody.RSTOP //"RSTOP"
                            , _getArrInfoByStopID.msgBody.STOP_ID //"CP_STOP_ID"
                            , _getArrInfoByStopID.msgBody.STOP_NAME //"CP_STOP_NAME"
                            , "S-OPERATION-COUNT"
                            , _getArrInfoByStopID.msgBody.ALLOC_TIME // "ALLOC_TIME"
                            , "Now-Time"
                            , _getArrInfoByStopID.msgBody.START_TIME //"START TIME"
                            , _getArrInfoByStopID.msgBody.END_TIME //"END TIME"
                            , "WRITE-CODE");
                    }
                }
            }

            foreach (XmlNode xn in xml_node) // Query결과를 Dictionary자료구조에 저장
            {
                string nRSTOP = ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.RSTOP]);
                if (nRSTOP == "기점")
                    continue;

                if (QueryRstop.ContainsKey(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID]))) // 동시에 운행되고 있는 다른회차의 노선번호
                {
                    QueryRstop[ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID])].Add(xn);
                }
                else  // 새로 본 노선번호
                {
                    List<XmlNode> OneRouteList = new List<XmlNode>();
                    OneRouteList.Add(xn);
                    QueryRstop.Add(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID]), OneRouteList);
                }
            }


            if (isWriteCSVFile)
                using (StreamWriter outputFile = new StreamWriter(csvFileName, true, Encoding.UTF8))
            {
                foreach (KeyValuePair<string, List<XmlNode>> keyValuePair in QueryRstop) // 버스도착정보 업데이트
                {
                    if (rstop.ContainsKey(keyValuePair.Key)) // 기존에 기억하고 있는 노선번호
                    {
                        List<int> remove_reservation_rstop = new List<int>();

                        // 기존 회차 노선들
                        for (int i = 0, index = 0, Count = rstop[keyValuePair.Key].Count/*keyValuePair.Value.Count*/; i < Count; i++)
                        {
                            //rstop[keyValuePair.Key][i][getArrInfoByStopID.msgBody.ROUTE_ID].InnerText = "325000001" // 2번;
                            /* DEBUG1234
                             * List<XmlNode> rstop_keyValuePair_Key = rstop[keyValuePair.Key];
                            int cc = rstop_keyValuePair_Key.Count;
                            XmlNode xn11 = rstop_keyValuePair_Key[i];
                            List<XmlNode> xmlNodes11 = keyValuePair.Value;*/
                            index = Define_WhichClosestRoute(rstop[keyValuePair.Key][i], keyValuePair.Value);


                            if (index < 0) // Query를 사용하지 않는 구문
                            {
                                // 해당 정류장을 지나감(해당 정류장을 지나가면 해당 회자의 노선번호가 사라짐)
                                // 도착한 노선 삭제

                                // 비고 : 기점, 종점 같은 경우(아직 운행전이기 때문에, 계산할 필요가 없음) - 처리 안함.
                                if (ConvertStrToNaturalNumber(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.RSTOP])) == 1)
                                {
                                    // WriteLine(도착-4)
                                    BusStop_keyValuePair = Get_RSTOP_distance(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                        , ConvertStrToNaturalNumber(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.STOP_ID]))
                                        , ConvertStrToNaturalNumber(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.RSTOP])));


                                        if (isWriteCSVFile)
                                            outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}"
                                        , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                        , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_NAME])
                                        , 0/*ConvertNodeToString(rstop[keyValuePair.Key][i][getArrInfoByStopID.msgBody.RSTOP])*/
                                        , BusStop_keyValuePair.Key
                                        , BusStop_keyValuePair.Value
                                        , rstop[keyValuePair.Key].Count
                                        , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ALLOC_TIME])
                                        , DateTime.Now.ToString("HH:mm:ss")
                                        , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.START_TIME])
                                        , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.END_TIME])
                                        , 4);
#if DEBUG1234
                                    consoleColor = Console.ForegroundColor;
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("(도착) Route ID : {0}, ROUTE_NAME : {1}번,RSTOP : {2}, STOP ID : {3}, STOP NAME : {4}, Date Time : {5}, 동시에 운행되고 있는 수 : {6}",
                                        ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                        , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_NAME])
                                        , 0/*ConvertNodeToString(rstop[keyValuePair.Key][i][getArrInfoByStopID.msgBody.RSTOP])*/
                                        , BusStop_keyValuePair.Key
                                        , BusStop_keyValuePair.Value
                                        , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                        , rstop[keyValuePair.Key].Count);
                                    Console.ForegroundColor = consoleColor;
#endif
                                    //rstop[keyValuePair.Key].RemoveAt(i); // 해당 노선에서 i번째에 있는 노선 제거
                                    remove_reservation_rstop.Add(i);
                                    //if (rstop[keyValuePair.Key].Count < 1)
                                    //    rstop.Remove(keyValuePair.Key); // 해당 노선에 대한 정보가 없을 경우, Dictionary에서 해당 노선 제거
                                    //if (keyValuePair.Value.Count > 0)
                                    //    keyValuePair.Value.RemoveAt(i); // 사용한 query 제거 -> 도착하면 해당 정류장에 대한 Query는 사라지기 때문에, Query를 사용하지 않은 것과 같음

                                    //continue;

                                    if (results.ContainsKey("도착"))
                                    {
                                        if (results["도착"].ContainsKey(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])))
                                            results["도착"][ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])].Add(rstop[keyValuePair.Key][i]/*동시에 운행되고 있는 회차들*/);
                                        else
                                        {
                                            List<XmlNode> OneCycleRouteLists = new List<XmlNode>(); // 동시에 운행되고 있는 회차들.
                                            OneCycleRouteLists.Add(rstop[keyValuePair.Key][i]);

                                            results["도착"].Add(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID]), OneCycleRouteLists);
                                        }
                                    }
                                    else
                                    {
                                        List<XmlNode> OneCycleRouteLists = new List<XmlNode>(); // 동시에 운행되고 있는 회차들.
                                        OneCycleRouteLists.Add(rstop[keyValuePair.Key][i]);

                                        Dictionary<string, List<XmlNode>> RouteList = new Dictionary<string, List<XmlNode>>(); // 복수의 ROUTE_ID들이 동시에 운행되고 있는 회차들에 대한 정보.
                                        RouteList.Add(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID]), OneCycleRouteLists);

                                        results.Add("도착", RouteList);
                                    }
                                }
                            }
                            else // Query를 사용하는 구문
                            // index : Query용 keyValuePair에서 인덱스로 사용하기위한 rstop[keyValuePair.Key]의 정보를 대응하기 위한 인덱스(예: 2번버스 32회차)
                            {
                                // 운행되고 있는 한 회차의 노선 수정
                                memory_rstop = ConvertStrToNaturalNumber(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.RSTOP]));
                                count = keyValuePair.Value.Count;
                                query_rstop = ConvertStrToNaturalNumber(ConvertNodeToString(keyValuePair.Value[index][_getArrInfoByStopID.msgBody.RSTOP]));

                                if (memory_rstop != query_rstop)
                                {
                                    rstop[keyValuePair.Key][i] = keyValuePair.Value[index]; // RSTOP 업데이트

                                    // WriteLine(2)
                                    BusStop_keyValuePair = Get_RSTOP_distance(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                        , ConvertStrToNaturalNumber(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.STOP_ID]))
                                        , ConvertStrToNaturalNumber(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.RSTOP])));


                                        if (isWriteCSVFile)
                                            outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}"
                                         , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                         , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_NAME])
                                         , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.RSTOP])
                                         , BusStop_keyValuePair.Key
                                         , BusStop_keyValuePair.Value
                                         , rstop[keyValuePair.Key].Count
                                         , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ALLOC_TIME])
                                         , DateTime.Now.ToString("HH:mm:ss")
                                         , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.START_TIME])
                                         , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.END_TIME])
                                         , 2);
#if DEBUG1234
                                    consoleColor = Console.ForegroundColor;
                                    Console.ForegroundColor = ConsoleColor.Magenta;
                                    Console.WriteLine("Route ID : {0}, ROUTE_NAME : {1}번,RSTOP : {2}, STOP ID : {3}, STOP NAME : {4}, Date Time : {5} 동시에 운행되고 있는 수 : {6}",
                                        ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                        , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_NAME])
                                        , ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.RSTOP])
                                        , BusStop_keyValuePair.Key
                                        , BusStop_keyValuePair.Value
                                        , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                        , rstop[keyValuePair.Key].Count);
                                    Console.ForegroundColor = consoleColor;
#endif

                                    if (results.ContainsKey("이동"))
                                    {
                                        if (results["이동"].ContainsKey(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])))
                                            results["이동"][ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID])].Add(rstop[keyValuePair.Key][i]/*동시에 운행되고 있는 회차들*/);
                                        else
                                        {
                                            List<XmlNode> OneCycleRouteLists = new List<XmlNode>(); // 동시에 운행되고 있는 회차들.
                                            OneCycleRouteLists.Add(rstop[keyValuePair.Key][i]);

                                            results["이동"].Add(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID]), OneCycleRouteLists);
                                        }
                                    }
                                    else
                                    {
                                        List<XmlNode> OneCycleRouteLists = new List<XmlNode>(); // 동시에 운행되고 있는 회차들.
                                        OneCycleRouteLists.Add(rstop[keyValuePair.Key][i]);

                                        Dictionary<string, List<XmlNode>> RouteList = new Dictionary<string, List<XmlNode>>(); // 복수의 ROUTE_ID들이 동시에 운행되고 있는 회차들에 대한 정보.
                                        RouteList.Add(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.ROUTE_ID]), OneCycleRouteLists);

                                        results.Add("이동", RouteList);
                                    }
                                }

                                if (keyValuePair.Value.Count > 0)
                                    keyValuePair.Value.RemoveAt(index); // 해당 Query는 사용완료된 데이터로써 나중에 사용할 필요가 없는 데이터.
                            }
                        }

                        // 기존 회차에서 삭제 예약한 것들 시행(삭제).
                        foreach (int removeIndex in remove_reservation_rstop)
                            rstop[keyValuePair.Key].RemoveAt(removeIndex); // 해당 노선에서 i번째에 있는 노선 제거
                                                                           // (rstop[keyValuePair.Key] - List형을 for문의 i라는 인덱스로 접근함으로써 발생하는 문제
                                                                           // : i번째 인덱스의 데이터를 삭제를 하게 되면 n번째,n-1번째 등에 접근할 수가 없기 때문에 따로 기억해두었다가 삭제하는 구문이다)

                        // 새 회차의 노선 추가
                        for (int i = 0; i < keyValuePair.Value.Count; i++)
                        {
                            rstop[keyValuePair.Key].Add(keyValuePair.Value[i]);

                            // WriteLine(3)
                            BusStop_keyValuePair = Get_RSTOP_distance(ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                            , ConvertStrToNaturalNumber(ConvertNodeToString(rstop[keyValuePair.Key][i][_getArrInfoByStopID.msgBody.STOP_ID]))
                                            , ConvertStrToNaturalNumber(ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.RSTOP])));


                                if (isWriteCSVFile)
                                    outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}"
                                            , ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                            , ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_NAME])
                                            , ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.RSTOP])
                                            , BusStop_keyValuePair.Key
                                            , BusStop_keyValuePair.Value
                                            , rstop[keyValuePair.Key].Count
                                            , ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ALLOC_TIME])
                                            , DateTime.Now.ToString("HH:mm:ss")
                                            , ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.START_TIME])
                                            , ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.END_TIME])
                                            , 3);

#if DEBUG1234
                            consoleColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine("(기존에 본 노선중에 운행되고 있는 새 회차) Route ID : {0}, ROUTE_NAME : {1}번,RSTOP : {2}, STOP ID : {3}, STOP NAME : {4}, Date Time : {5} 동시에 운행되고 있는 수 : {6}",
                                        ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_ID])
                                        , ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_NAME])
                                        , ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.RSTOP])
                                        , BusStop_keyValuePair.Key
                                        , BusStop_keyValuePair.Value
                                        , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                        , rstop[keyValuePair.Key].Count);
                            Console.ForegroundColor = consoleColor;
#endif

                            if (results.ContainsKey("회차추가"))
                            {
                                if (results["회차추가"].ContainsKey(ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_ID])))
                                    results["회차추가"][ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_ID])].Add(keyValuePair.Value[i]/*동시에 운행되고 있는 회차들*/);
                                else
                                {
                                    List<XmlNode> OneCycleRouteLists = new List<XmlNode>(); // 동시에 운행되고 있는 회차들.
                                    OneCycleRouteLists.Add(keyValuePair.Value[i]);

                                    results["회차추가"].Add(ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_ID]), OneCycleRouteLists);
                                }
                            }
                            else
                            {
                                List<XmlNode> OneCycleRouteLists = new List<XmlNode>(); // 동시에 운행되고 있는 회차들.
                                OneCycleRouteLists.Add(keyValuePair.Value[i]);

                                Dictionary<string, List<XmlNode>> RouteList = new Dictionary<string, List<XmlNode>>(); // 복수의 ROUTE_ID들이 동시에 운행되고 있는 회차들에 대한 정보.
                                RouteList.Add(ConvertNodeToString(keyValuePair.Value[i][_getArrInfoByStopID.msgBody.ROUTE_ID]), OneCycleRouteLists);

                                results.Add("회차추가", RouteList);
                            }
                        }


                        if (rstop[keyValuePair.Key].Count < 1)
                            rstop.Remove(keyValuePair.Key); // 해당 노선에 대한 정보가 없을 경우, Dictionary에서 해당 노선 제거
                    }
                    else  // 새로 본 노선번호
                    {
                        rstop.Add(keyValuePair.Key, keyValuePair.Value);

                        // WriteLine(1)
                        foreach (XmlNode xn in keyValuePair.Value)
                        {
                            BusStop_keyValuePair = Get_RSTOP_distance(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID])
                                            , ConvertStrToNaturalNumber(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.STOP_ID]))
                                            , ConvertStrToNaturalNumber(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.RSTOP])));



                                if (isWriteCSVFile)
                                    outputFile.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}"
                                            , ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID])
                                            , ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_NAME])
                                            , ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.RSTOP])
                                            , BusStop_keyValuePair.Key
                                            , BusStop_keyValuePair.Value
                                            , rstop[keyValuePair.Key].Count
                                            , ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ALLOC_TIME])
                                            , DateTime.Now.ToString("HH:mm:ss")
                                            , ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.START_TIME])
                                            , ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.END_TIME])
                                            , 1);
#if DEBUG1234
                            consoleColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("(새로 본 노선 번호들) Route ID : {0}, ROUTE_NAME : {1}번,RSTOP : {2}, STOP ID : {3}, STOP NAME : {4}, Date Time : {5} 동시에 운행되고 있는 수 : {6}",
                             ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID])
                             , ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_NAME])
                             , ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.RSTOP])
                             , BusStop_keyValuePair.Key
                             , BusStop_keyValuePair.Value
                             , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                             , rstop[keyValuePair.Key].Count);
                            Console.ForegroundColor = consoleColor;
#endif

                            if (results.ContainsKey("노선추가"))
                            {
                                if (results["노선추가"].ContainsKey(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID])))
                                    results["노선추가"][ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID])].Add(xn/*동시에 운행되고 있는 회차들*/);
                                else
                                {
                                    List<XmlNode> OneCycleRouteLists = new List<XmlNode>(); // 동시에 운행되고 있는 회차들.
                                    OneCycleRouteLists.Add(xn);

                                    results["노선추가"].Add(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID]), OneCycleRouteLists);
                                }
                            }
                            else
                            {
                                List<XmlNode> OneCycleRouteLists = new List<XmlNode>(); // 동시에 운행되고 있는 회차들.
                                OneCycleRouteLists.Add(xn);

                                Dictionary<string, List<XmlNode>> RouteList = new Dictionary<string, List<XmlNode>>(); // 복수의 ROUTE_ID들이 동시에 운행되고 있는 회차들에 대한 정보.
                                RouteList.Add(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.ROUTE_ID]), OneCycleRouteLists);

                                results.Add("노선추가", RouteList);
                            }
                        }
                    }
                }

#if DEBUG1234
                Console.WriteLine("\n\n**************************************************************\n");
#endif
            }

            return results;
        }


        private int real_intervalTime = -1;
        private int TaskCount = 0;
        private bool doTask = true;
        List<Dictionary<string, List<XmlNode>>> rstopList = new List<Dictionary<string, List<XmlNode>>>();
        private void TaskAction(ref int taskCount, ref int _real_intervalTime, ref bool doTask, string ROUTE_ID)
        {
            int connection_interval_time = 10 * 1000; // default : 10*1000ms
            Stopwatch stopwatch = new Stopwatch();

            Dictionary<string, List<XmlNode>> rstop = new Dictionary<string, List<XmlNode>>();
            using (Mutex m = new Mutex(false, "MutexName_DayReset"))
            {
                rstopList.Add(rstop);
            }
            String xml;

            while (doTask)
            {
                if (!((_startTime.Hour * 60 + _startTime.Minute <= DateTime.Now.Hour * 60 + DateTime.Now.Minute)
                        && (DateTime.Now.Hour * 60 + DateTime.Now.Minute <= _endTime.Hour * 60 + _endTime.Minute) && !connection_count_excess))
                {
                    int NowTime_Minutes, _startTime_Minutes;
                    TimeSpan time;
                    string str;

#if !NoSort
                    Console.WriteLine("대기하기 전에 먼저 \"MakeBusesArrivalHistory\"에서 금일에 생성한 CSV을 정렬합니다.");

                    if (String.IsNullOrEmpty(csvDirPath))
                        csvDirPath = String.Format("{0}\\{1}년 {2}월", "Bus Information for Big Data", DateTime.Now.Year, DateTime.Now.Month);

                    List<System.IO.FileInfo> fileLists = GetFileLists(csvDirPath);
                    System.IO.FileInfo fileInfo = null;
                    if (fileLists != null && fileLists.Count > 0)
                    {
                        for (int l = 0; l < fileLists.Count; l++)
                        {
                            if (fileLists[l].Name.Contains(String.Format("STOP_ID - {0}", ROUTE_ID/*STOP_ID*/)))
                                fileInfo = fileLists[l];
                        }
                        if (fileInfo == null)
                        {
                            Console.WriteLine("금일에 생성한 파일을 찾을 수 없습니다.");
#if DEBUG1234
                            Console.WriteLine("(CSV파일 이름은 \"STOP_ID - {0}\"와 같이 \"STOP_ID가 무조건 포함이 되어 있어야합니다.\")", ROUTE_ID);
#endif
                        }
                        else
                        {
                            BusHistory_CSVFile_sort(fileInfo.FullName);
                            Console.WriteLine("정렬을 완료하였습니다.");
                        }
                    }
                    else
                        Console.WriteLine("경로에 파일이 하나도 존재하지 않습니다.");
#endif

                    time = TimeSpan.FromSeconds((NowTime_Minutes = (DateTime.Now.Hour * 60 + DateTime.Now.Minute)) <= (_startTime_Minutes = (_startTime.Hour * 60 + _startTime.Minute)) ?
                        (_startTime_Minutes - NowTime_Minutes) * 60 : (_startTime_Minutes + (1440 - NowTime_Minutes)) * 60);
                    str = time.ToString(@"hh\시mm\분");

                    if (connection_count_excess)
                        Console.Write("서버 연결회수가 초과했기 때문에, ");
                    else
                        Console.Write("현재 설정된 시간이외의 시간이기 때문에, ");
                    Console.WriteLine(str + " 동안 대기합니다.");
                    Thread.Sleep(((time.Minutes + time.Hours * 60) * 60) * 1000);

                    rstop.Clear();
                }
                using (Mutex m = new Mutex(false, "MutexName_WriteArrivalHistory"))
                    xml = GetXML(getArrInfoByStopID.url_of_Base, ROUTE_ID);

                stopwatch.Restart();
                if (!string.IsNullOrEmpty(xml))
                {
                    // using (Mutex m = new Mutex(false, "MutexName_WriteArrivalHistory"))
                    MakeBusesArrivalHistory(xml, ref rstop);
                }
                connection_interval_time = TaskCount * _real_intervalTime;
                stopwatch.Stop();
                if (connection_interval_time > stopwatch.ElapsedMilliseconds && doTask/*Signal발생시 즉각 중지*/)
                    Thread.Sleep(connection_interval_time - (int)stopwatch.ElapsedMilliseconds);
            }

            return;
        }
        /// <summary>
        /// 버스정보를 서버에서 최대로 연결할 수 있는 회수안에서 주기를 자동으로 설정합니다.
        /// </summary>
        /// <param name="STOP_ID">STOP_ID ID</param>
        /// <returns>MakeBusesArrivalHistory함수의 동작을 수행하는 Task</returns>
        public Task MakeTask_ArrivalHistory(string STOP_ID)
        {
            double minute = (endTime.Minute - startTime.Minute), second = (endTime.Second - startTime.Second);
            double result = -1;
            minute /= 60;
            minute = Math.Round(minute, 2);
            second /= 3600;
            second = Math.Round(second, 2);


            /*
             *  20000(T) - 18(H)*3600/x > 0 --> 18(H)*3600/20000(T) < x
             *  Fomula : H*3600/T < x (H*3600을 "시간+분+초"로 변환할 수 있으며, "시간+분/60+초/3600"이라는 식으로 변환하면 된다.
             *  Large X : (x * 1000) * n ('Large X' 단위 : ms)
             */
            using (Mutex m = new Mutex(false, "MutexName3"))
            {
                result = (endTime.Hour - startTime.Hour + minute + second) * 3600;
                result /= (_total_available_Connection_count - Connection_count);
                result = Math.Round(result, 2) * 1000 + 10;
                real_intervalTime = (int)result;

                TaskCount++;
            }

#if DEBUG1234
            if (GetReal_intervalTime() > 10 * 1000)
            {
                Console.WriteLine("MakeTask_ArrivalHistory함수 메세지 : 서버에 연결하는 시간주기는 10초를 넘지않는 것이 좋습니다.");
                Console.WriteLine("(interval time : {0}, Task Count : {1})", GetReal_intervalTime(), TaskCount);
                Thread.Sleep(500);
            }
#endif
            Action SomeAction = delegate () { TaskAction(ref TaskCount, ref real_intervalTime, ref doTask, STOP_ID); };

            return new Task(SomeAction);
        }

        /// <summary>
        /// 버스정보를 기록하는 태스크(MakeTask_ArrivalHistory함수에서 생성한 Task)를 중단하도록 합니다.
        /// </summary>
        public void Stop_TaskArrivalHistory()
        {
            doTask = false;
            TaskCount--;
        }

        /// <summary>
        /// MakeTask_ArrivalHistory함수에 의해 계산된 서버 연결시간 간격을 반환합니다.
        /// </summary>
        /// <returns>시간단위 : ms(밀리 초)</returns>
        public int GetReal_intervalTime()
        {
            return real_intervalTime * TaskCount;
        }
        // *************************************************************************************************************************************
        //                                      XML을 받아오는 함수들을 가지고 활용할 수 있는 함수들

        /// <summary>
        /// 해당 정류장을 경유하는 버스 목록을 csv파일로 생성합니다.
        /// </summary>
        /// <param name="BusNames">GetBusNames함수의 반환값을 전달하면 됩니다.</param>
        /// <param name="BusStopNames">GetBusStopNames함수의 반환값을 전달하면 됩니다.</param>
        /// <param name="passingBusStop">GetPassingBy_BusStopNames_for_Buses함수의 반환값을 전달하면 됩니다.</param>
        /// <param name="DirPath">해당 정류장을 경유하는 버스 목록이 적인 csv파일이 생성될 "경로의 이름"입니다.</param>
        public void WriteCSV_for_PassingBy_TheBusStop(Dictionary<string, string> BusNames, Dictionary<string, string> BusStopNames
            , Dictionary<string, List<KeyValuePair<string, string>>> passingBusStop
            , string DirPath = "해당 정류장을 경유하는 버스 목록", bool isWriteCSVFile = false)
        {
            DirPath = "C:\\Users\\Administrator\\Documents\\" + DirPath;
            DirectoryInfo di = new DirectoryInfo(DirPath);
            if (di.Exists == false)
            {
                di.Create();
            }

            foreach (KeyValuePair<string, List<KeyValuePair<string, string>>> kv in passingBusStop)
            {
                //Console.WriteLine("{0},{1},{2}", kv.Key, BusStopNames[kv.Key], kv.Value.Count);
                string filename = DirPath + "\\" + BusStopNames[kv.Key] + "  (Bus Stop ID " + kv.Key + ").csv";

                
                    if(isWriteCSVFile)
                    using (StreamWriter outputFile = new StreamWriter(filename, false/*no append*/, Encoding.UTF8))
                {
                    outputFile.WriteLine("Bus ID,Bus Name");

                    foreach (KeyValuePair<string, string> bus in kv.Value)
                    {
                        outputFile.WriteLine("{0},{1}", bus.Key, bus.Value);
                    }
                }
            }
        }

        // *************************************************************************************************************************************

        public bool connection_count_excess = false; // 더이상 연결할 수 없으면 true, 그렇지 않으면 false
        private int ErrorConnection_try_count = 0, Connection_count = 0;
        /// <summary>
        /// 여수시 버스 정보의 XML을 받아오기 위한 함수 입니다.
        /// 정류장버스도착정보, 버스정류장정보, 버스위치정보, 버스운행정보 등의 XML을  받아올 수 있습니다.
        /// 
        /// Class를 사용해서 url을 GetXML함수에 전달할 때 "url"이 아닌, "url_of_base"라는 것이 들어가면 추가적인 attribute(ID)를 반드시 적어주어야합니다.
        /// </summary>
        /// <param name="XML_URL">XML을 받기 위한 URL</param>
        /// <param name="ID">
        /// XML_URL에서 변수이름에서 "url_of_Base"라는 내용이 들어갈 경우 적어주어야하는 인자
        /// 
        /// 예시
        /// Case 1. GetXML(getStaionByRouteAll.url)
        /// Case 2. GetXML(getArrInfoByStopID.url_of_Base, "325000107"/*ROUTE_ID*/)
        /// Case 3. GetXML(getArrInfoByStopID.url_of_Base, "325000541"/*STOP_ID*/)
        /// </param>
        /// <returns>
        /// 응답 메세지 XML이 string형으로 반환됨.
        /// Error 발생시 아무값을 포함하지 않는 String.Empty이 반환되고, 정상적인 경우 docx문서에 나오는 응답 메세지 예제와 같음
        /// </returns>
        public string GetXML(string XML_URL, string ID = "")
        {
            string url = XML_URL + ID;
            int numOfRows = 0, totalCount = 0;
            // Console.WriteLine("URL : " + url);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";

            string results = "";
            HttpWebResponse response;


            String xml_test = "";
            List<String> xml_bundle = new List<String>();
            do
            {
                try
                {
                    using (response = request.GetResponse() as HttpWebResponse)
                    {
                        StreamReader reader = new StreamReader(response.GetResponseStream());
                        xml_test = reader.ReadToEnd();
                        xml_bundle.Add(xml_test);
                    }
                }
                catch (WebException e)
                {
                    Console.WriteLine("웹서버에서 Response를 받는데 실패하였습니다. 이는 인터넷 상태가 정상인 경우 자주 있는 일은 아니므로 잠시후 다시 시도 하시길 바랍니다. (Error Code : {0})", e.Message);
                    return null;
                }

                using (Mutex m = new Mutex(false, "MutexName4"))
                {
                    ErrorConnection_try_count++;

                    // 에러검사1 ("returnAuthMsg")
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(xml_test);
                    XmlNodeList xnList_err = xmlDoc.GetElementsByTagName("cmmMsgHeader"); //접근할 노드
                    if (xnList_err.Count > 0)
                    {
                        string errorMSG = String.Format("정상적으로 처리되지못했습니다.\nError Message : {0}(errMsg), {1}(returnAuthMsg)"
                            , ConvertNodeToString(xnList_err[0]["errMsg"]), ConvertNodeToString(xnList_err[0]["returnAuthMsg"]));
                        Console.WriteLine(errorMSG); // LIMITED_NUMBER_OF_SERVICE_REQUESTS_EXCEEDS_ERROR
                        if (ConvertNodeToString(xnList_err[0]["errMsg"]).Contains("LIMITED_NUMBER_OF_SERVICE_REQUESTS_EXCEEDS_ERROR") || ErrorConnection_try_count > 10)
                        {
                            connection_count_excess = true;
                        }

                        return String.Empty;
                    }

                    ErrorConnection_try_count = 0;
                    Connection_count++;

                    // 에러검사2 ("resultMsg")
                    XmlNodeList xnList = XmlParser_for_msgHeader(xml_test);
                    if (xnList.Count > 0 && ConvertNodeToString(xnList[0]["resultMsg"]) != "정상적으로 처리되었습니다.")
                    {
                        string errorMSG = String.Format("정상적으로 처리되지못했습니다.\nError Message : {0}", ConvertNodeToString(xnList[0]["resultMsg"]));
                        Console.WriteLine(errorMSG);

                        return String.Empty;
                    }

                    // Local변수이지만 "에러검사2"에서 parser한 값을 재활용하기 위해 여기에 씀
                    if (numOfRows == 0)// 
                        totalCount = ConvertStrToNaturalNumber(ConvertNodeToString(xnList[0]["totalCount"]));

                    numOfRows += ConvertStrToNaturalNumber(ConvertNodeToString(xnList[0]["numOfRows"]));
                }
            } while (numOfRows < totalCount);
            results = MergeXml(xml_bundle.ToArray());

            //Console.WriteLine(results);

            Thread.Sleep(5); // 서버에서 DoS공격이 아니라고 판단하게 하기 위함.
            // connection_count++;

            return results;
        }

        /// <summary>
        /// string으로 되어 있는 xml을 Parser해서 XmlNodeList로 변환시켜줌
        /// 
        /// </summary>
        /// <param name="xml">GetXML함수에서 (오류가 발생하지 않은) 반환한 값을 이 인자에 전달해주면 됨</param>
        /// <returns>
        /// 반환된 XmlNodeList는 배열처럼 사용할 수 있음
        /// 예시
        /// : string numOfRows = ConvertNodeToString(xn["msgHeader"]["numOfRows"]);
        /// </returns>
        static public XmlNodeList XmlParser(string xml)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            XmlNodeList xnList = xmlDoc.GetElementsByTagName("ServiceResult"); //접근할 노드

            /*string temp = string.Empty;
            foreach (XmlNode xn in xnList)
            {
                string numOfRows = ConvertNodeToString(xn["msgHeader"]["numOfRows"]);
                string pageNo = ConvertNodeToString(xn["msgHeader"]["pageNo"]);
                string resultCode = ConvertNodeToString(xn["msgHeader"]["resultCode"]);
                string resultMsg = ConvertNodeToString(xn["msgHeader"]["resultMsg"]);
                string totalCount = ConvertNodeToString(xn["msgHeader"]["totalCount"]);

                string ROUTE_NAME = ConvertNodeToString(xn["msgBody"]["ROUTE_NAME"]);
                string RSTOP = ConvertNodeToString(xn["msgBody"]["RSTOP"]);
                string STOP_NAME = ConvertNodeToString(xn["msgBody"]["STOP_NAME"]);

                temp += "ROUTE_NAME : " + ROUTE_NAME + ", RSTOP : " + RSTOP + ", STOP_NAME : " + STOP_NAME + "\n";
                // else...
            }
            Console.WriteLine("temp => "+temp);*/

            return xnList;
        }

        /// <summary>
        /// string으로 되어 있는 xml을 Parser해서 XmlNodeList로 변환시켜줌
        /// </summary>
        /// <param name="xml">GetXML함수의 정상적인 반환한 값을 이 인자에 전달해주면 됨</param>
        /// <returns>
        /// 반환된 XmlNodeList는 배열처럼 사용할 수 있음
        /// 
        /// 예시:
        ///   string ROUTE_NAME = ConvertNodeToString(xn[getArrInfoByStopID.msgBody.ROUTE_NAME]);
        /// </returns>
        static public XmlNodeList XmlParser_for_itemList(string xml)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            XmlNodeList xnList = xmlDoc.GetElementsByTagName("itemList"); //접근할 노드


            //string temp = string.Empty;
            //foreach (XmlNode xn in xnList)
            //{
            //    string ROUTE_ID = ConvertNodeToString(xn["ROUTE_ID"]);
            //    string ROUTE_NAME = ConvertNodeToString(xn["ROUTE_NAME"]);
            //    string RSTOP = ConvertNodeToString(xn["RSTOP"]);
            //    string STOP_NAME = ConvertNodeToString(xn["STOP_NAME"]);
            //    Console.WriteLine("ROUTE_NAME : {0}, ROUTE_NAME : {1}, RSTOP : {2}, STOP_NAME : {3}", ROUTE_ID, ROUTE_NAME, RSTOP, STOP_NAME);
            //    //temp += "ROUTE_NAME : " + ROUTE_NAME + ", RSTOP : " + RSTOP + ", STOP_NAME : " + STOP_NAME + "\n";
            //    // else...
            //}
            /*Console.WriteLine("temp => "+temp);*/

            return xnList;
        }

        /// <summary>
        /// string으로 되어 있는 xml을 Parser해서 XmlNodeList로 변환시켜줌 - msgHeader부분만 Parser 동작을 수행함.
        /// </summary>
        /// <param name="xml">GetXML함수에서 (오류가 발생하지 않은) 반환한 값을 이 인자에 전달해주면 됨</param>
        /// <returns>
        /// 반환된 XmlNodeList는 배열처럼 사용할 수 있음
        /// 예시
        /// : string resultCode = ConvertNodeToString(xn[getRouteInfoAll.msgHeader.resultCode])
        /// </returns>
        static public XmlNodeList XmlParser_for_msgHeader(string xml)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            XmlNodeList xnList = xmlDoc.GetElementsByTagName("msgHeader"); //접근할 노드

            /*string temp = string.Empty;
            foreach (XmlNode xn in xnList)
            {
                string numOfRows = ConvertNodeToString(xn["numOfRows"]);
                string pageNo = ConvertNodeToString(xn["pageNo"]);
                string resultCode = ConvertNodeToString(xn["resultCode"]);
                string resultMsg = ConvertNodeToString(xn["resultMsg"]);
                string totalCount = ConvertNodeToString(xn["totalCount"]);
                temp += "numOfRows : " + numOfRows + ", pageNo : " + pageNo + ", resultCode : " + resultCode
                    + ", resultMsg : " + resultMsg + ", totalCount : " + totalCount + "\n";
            }
            Console.WriteLine("temp => " + temp);*/

            return xnList;
        }

        /// <summary>
        /// 이 클래스의 함수(XmlParser, XmlParser_for_itemList,XmlParser_for_msgHeader)의 반환값(노드)의 String을 가져옴.
        /// </summary>
        /// <param name="xmlNode">이 클래스의 함수(XmlParser, XmlParser_for_itemList,XmlParser_for_msgHeader)의 반환값(노드)</param>
        /// <returns>반환값(노드)의 String</returns>
        static public string ConvertNodeToString(XmlNode xmlNode)
        {
            if (xmlNode == null)
            {
                //Console.WriteLine("Converted");
                return "";
            }
            else
                return xmlNode.InnerText;
        }

        /// <summary>
        /// 문자를 숫자로 변환함(String에 숫자 외의 값은 생략되서 변환됨)
        /// 
        /// RSTOP을 숫자로 변환하려고 만들었고, 주로 RSTOP변환용으로만 사용됨.
        /// </summary>
        /// <param name="str">0부터 자연수까지의 값을 전달</param>
        /// <returns>Error인경우 -1, 정상적인경우 -1이 아닌 숫자값</returns>
        static public int ConvertStrToNaturalNumber(string str)
        {
            try
            {
                str = Regex.Replace(str, @"\D", "");
            }
            catch (InvalidOperationException e)
            {
                str = Regex.Replace(str, @"\D", "");
            }

            if (string.IsNullOrEmpty(str))
                return -1;
            else
                return Convert.ToInt32(str);
        }

        /// <summary>
        /// "전라남도 여수시_버스정보>정류장 버스 도착 정보 조회>정류소ID 별 버스 도착예정정보 조회 서비스"의 XML을 병합하여, 하나의 XML파일로 만듭니다.
        /// </summary>
        /// <param name="xml">병합할 XML들</param>
        /// <returns>병합된 XML</returns>
        private static String MergeXml(params string[] xml)
        {
            string body_temp, numOfRows_str, pageNo_str
                , head = xml[0].Substring(0, xml[0].IndexOf("<msgBody>") + "<msgBody>".Length)
            , body = (body = xml[0].Substring(xml[0].IndexOf("<msgBody>") + "<msgBody>".Length)).Substring(0, body.IndexOf("</msgBody>"))
            , tail = xml[0].Substring(xml[0].IndexOf("</msgBody>"));
            int numOfRows, pageNo;

            numOfRows_str = (numOfRows_str = (head.Substring(head.IndexOf("<numOfRows>") + "<numOfRows>".Length))).Substring(0, numOfRows_str.IndexOf("</numOfRows>"));
            numOfRows = BusInfo.ConvertStrToNaturalNumber(numOfRows_str);
            pageNo_str = (pageNo_str = (head.Substring(head.IndexOf("<pageNo>") + "<pageNo>".Length))).Substring(0, pageNo_str.IndexOf("</pageNo>"));
            pageNo = BusInfo.ConvertStrToNaturalNumber(pageNo_str);


            for (int i = 1; i < xml.Length; i++)
            {

                numOfRows_str = (numOfRows_str = (xml[i].Substring(0, xml[i].IndexOf("<msgBody>") + "<msgBody>".Length).Substring(xml[i].Substring(0, xml[i].IndexOf("<msgBody>") + "<msgBody>".Length).IndexOf("<numOfRows>") + "<numOfRows>".Length))).Substring(0, numOfRows_str.IndexOf("</numOfRows>"));
                numOfRows += BusInfo.ConvertStrToNaturalNumber(numOfRows_str);
                pageNo_str = (pageNo_str = (xml[i].Substring(0, xml[i].IndexOf("<msgBody>") + "<msgBody>".Length).Substring(xml[i].Substring(0, xml[i].IndexOf("<msgBody>") + "<msgBody>".Length).IndexOf("<pageNo>") + "<pageNo>".Length))).Substring(0, pageNo_str.IndexOf("</pageNo>"));
                pageNo += BusInfo.ConvertStrToNaturalNumber(pageNo_str);

                body_temp = (body_temp = xml[i].Substring(xml[i].IndexOf("<msgBody>") + "<msgBody>".Length)).Substring(0, body_temp.IndexOf("</msgBody>"));
                body += body_temp;
            }

            head = head.Substring(0, head.IndexOf("<numOfRows>") + "<numOfRows>".Length)
                + numOfRows + head.Substring(head.IndexOf("</numOfRows>"));
            head = head.Substring(0, head.IndexOf("<pageNo>") + "<pageNo>".Length)
                + pageNo + head.Substring(head.IndexOf("</pageNo>"));

            return head + body + tail;
        }

        /// <summary>
        /// 레지스트리에 접근하기 위해 관리자 권한으로 실행되는지 확인한다.
        /// </summary>
        /// <returns>관리자 권한 실행여부</returns>
        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();

            if (identity != null)
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return false;
        }

        /// <summary>
        /// MakeBusesArrivalHistory함수에서 사용되는 것으로, XmlNode 자기자신을 찾는 역할을 한다.
        /// </summary>
        /// <param name="xn"></param>
        /// <param name="xmlNodes"></param>
        /// <returns></returns>
        static private int Define_WhichClosestRoute(XmlNode xn, List<XmlNode> xmlNodes)
        {
            int[] arr = new int[xmlNodes.Count];
            int i, max = -9999999, max_index, pointing_rstop = ConvertStrToNaturalNumber(ConvertNodeToString(xn[_getArrInfoByStopID.msgBody.RSTOP]));

            if (pointing_rstop == -1)
                throw new System.ArgumentException("Called from Define_WhichClosestRoute(), Message : There is no closest Route.", "None");
            // return -2; // RSTOP : 기점 혹은 종점.

            for (i = 0, max_index = 0; i < arr.Length; i++)
            {
                //xn1 = xmlNodes[i];
                //ConvertStrToNaturalNumber(ConvertNodeToString(xn1[getArrInfoByStopID.msgBody.RSTOP]));

                arr[i] = ConvertStrToNaturalNumber(ConvertNodeToString(xmlNodes[i][_getArrInfoByStopID.msgBody.RSTOP]))
                    - pointing_rstop;

                if (arr[i] == 0)
                    return i;

                if (-maxStopStep > arr[i] || 0 < arr[i])
                    arr[i] = -9999999; // 지정한 대상의 RSTOP이 절대 될수가 없음.

                if (arr[i] >= max)
                {
                    max = arr[i];
                    max_index = i;
                }
            }

            if (max == -9999999)
                return -1;
            else
                return max_index;
        }
        /// <summary>
        ///  BusHistory_CSVFile_sort함수에서 사용되는 것으로써 MakeBusesArrivalHistory함수에서 생성한 CSV파일을 정렬할 때 사용됨, 다음정류장이 무엇인지 찾는 역할을 한다.
        /// </summary>
        /// <param name="pointing_rstop">현재 RSTOP</param>
        /// <param name="rstop_index">CSV파일에서 RSTOP이 있는 Column(열) - A,B,C 일때 A를 찾으려면 0을 전달</param>
        /// <param name="route_id">찾을 ROUTE_ID</param>
        /// <param name="route_id_index">CSV파일에서 ROUTE_ID가 있는 Column(열) - A,B,C 일때 B를 찾으려면 1을 전달</param>
        /// <param name="lines">해당 파일을 읽은 정보가 담겨져있는 List로 아직 사용하지 않은 것을 의미. - 파일을 메모리에 한꺼번에 올리지 않기 위해 사용되어짐.</param>
        /// <returns>다음 정류장으로 간 회차(예 : 2번)가 담겨져 있는 라인</returns>
        static private int Define_WhichClosestRoute(int pointing_rstop, int rstop_index, int route_id, int route_id_index, int Line_Hash, int Line_Hash_index, List<string[]> lines) // sort용
        {
            List<KeyValuePair<int, string[]>> Nodes = new List<KeyValuePair<int, string[]>>(); // Key : lines에 있는 index, Value : 해당 route_id가 있는 line

            int[] arr;
            int i, max = -9999999, max_index;

            for (i = 0; i < lines.Count; i++)
            {
                if (ConvertStrToNaturalNumber(lines[i][route_id_index]) == route_id && ConvertStrToNaturalNumber(lines[i][Line_Hash_index]) > Line_Hash)
                {
                    Nodes.Add(new KeyValuePair<int, string[]>(i, lines[i]));
                }
            }
            arr = new int[Nodes.Count];

            if (pointing_rstop == -1)
                throw new System.ArgumentException("Called from Define_WhichClosestRoute(), Message : There is no closest Route.", "None");

            // return -2; // RSTOP : 기점 혹은 종점.

            for (i = 0, max_index = 0; i < arr.Length; i++)
            {
                //xn1 = xmlNodes[i];
                //ConvertStrToNaturalNumber(ConvertNodeToString(xn1[getArrInfoByStopID.msgBody.RSTOP]));

                arr[i] = ConvertStrToNaturalNumber(Nodes[i].Value[rstop_index])
                    - pointing_rstop;

                if (arr[i] == -1)
                    return Nodes[i].Key;

                if (-maxStopStep > arr[i] || 0 <= arr[i])
                    arr[i] = -9999999; // 지정한 대상의 RSTOP이 절대 될수가 없음.

                if (arr[i] > max)
                {
                    max = arr[i];
                    max_index = i;
                }
            }

            if (max == -9999999)
                return -1;
            else
                return Nodes[max_index].Key;
        }

        /// <summary>
        /// 지정한 ROUTE_ID가 현재 어느 정류장에 있는지를 알려줍니다.
        /// (서버에서 받아온 STOP_ID는 현재 정류장 ID가 아닌 Query한 STOP_ID를 의미하기 때문에 이 함수가 필요합니다.)
        /// </summary>
        /// <param name="Route_ID">ROUTE_ID</param>
        /// <param name="BusStop_ID">sTOP_ID</param>
        /// <param name="RSTOP">RSTOP</param>
        /// <returns>
        /// Key : STOP_ID
        /// Value : STOP_NAME
        /// 
        /// Error : KeyValuePair<Int32, string>(0, null)
        /// </returns>
        private KeyValuePair<Int32, string> Get_RSTOP_distance(string Route_ID, int BusStop_ID, int RSTOP)
        {
            List<KeyValuePair<Int32, string>> route;
            int distance = -1, i = 0;

            using (Mutex m = new Mutex(false, "MutexName1"))
            {
                if (!BusStop_Routes.ContainsKey(Route_ID))
                {
                    if ((route = GetBusStopes_moving(Route_ID)) == null)
                    {
                        if ((route = GetBusStopes_moving(Route_ID)) == null)
                            throw new ArgumentNullException("Called from Get_RSTOP_distance(), Message : Can't get Route(ROUTE_ID : " + Route_ID + ".", "None");
                        //return new KeyValuePair<Int32, string>(0, null);
                    }

                    try
                    {
                        BusStop_Routes.Add(Route_ID, route);
                    }
                    catch (ArgumentException e)
                    {

                    }
                }
            }

            foreach (KeyValuePair<Int32, string> keyValue in BusStop_Routes[Route_ID])
            {
                i++;
                if (keyValue.Key == BusStop_ID)
                {
                    distance = i;
                    break;
                }
            }

            if (distance == -1)
                return new KeyValuePair<Int32, string>(0, null);
            if (distance - RSTOP < 0)
            {
                String pos = checkPosition(Route_ID, BusStop_ID.ToString(), _ServiceKey);
                throw new Exception(String.Format("ROUTE_ID : {0}가 있는 정류장 구간을 찾을 수 없습니다.", Route_ID));
            }

            return BusStop_Routes[Route_ID][distance - RSTOP];
        }

        /// <summary>
        /// "버스 위치 정보 조회"가 신청되어 있어야함.
        /// </summary>
        /// <param name="ROUTE_ID">조회할 ROUTE_ID. 도착정보는 실행파일이 있는 곳에 XML으로 저장됨.</param>
        /// <param name="STOP_ID">조회할 ROUTE_ID가 도착할 때, 지정한 정류장의 도착정보를 위한 STOP_ID. 도착정보는 실행파일이 있는 곳에 XML으로 저장됨.</param>
        /// <param name="ServiceKey">서버에 접근하기 위한 서비스키값</param>
        /// <returns></returns>
        static public String checkPosition(String ROUTE_ID, String ServiceKey, String STOP_ID = null)
        {
            HttpWebRequest request1 = (HttpWebRequest)WebRequest.Create("http://apis.data.go.kr/4810000/busposinfo/getBusPosByRtid?serviceKey=" + ServiceKey + "&pageNo=1&numOfRows=9999&busRouteId=" + ROUTE_ID)
                , request2 = (HttpWebRequest)WebRequest.Create("http://apis.data.go.kr/4810000/arrive/getArrInfoByStopID?serviceKey=" + ServiceKey + "&pageNo=1&numOfRows=9999&busStopID=" + STOP_ID);
            request1.Method = "GET";
            request2.Method = "GET";
            String XML_Pos = "";
            int Count = 0; // 로그가 쌓이기 위한 변수

            HttpWebResponse response1 = request1.GetResponse() as HttpWebResponse, response2 = request2.GetResponse() as HttpWebResponse;

            using (StreamWriter outputFile = new StreamWriter("C:\\Users\\Administrator\\Documents\\" + String.Format("버스(ROUTE ID - {0}, {1}) 현재 위치.xml", ROUTE_ID, Count), false/*no append*/, Encoding.UTF8))
            {
                StreamReader reader = new StreamReader(response1.GetResponseStream());
                outputFile.WriteLine((XML_Pos = reader.ReadToEnd()));
            }
            if (STOP_ID != null)
                using (StreamWriter outputFile = new StreamWriter("C:\\Users\\Administrator\\Documents\\" + String.Format("정류장(STOP ID - {0}, {1}) 도착정보.xml", STOP_ID, Count), false/*no append*/, Encoding.UTF8))
                {
                    StreamReader reader = new StreamReader(response2.GetResponseStream());
                    outputFile.WriteLine(reader.ReadToEnd());
                }

            Count++;

            return XML_Pos;
        }

        /// <summary>
        /// CSV파일을 수정하는 용도로 만들어졌고, 기준점을 바꾸기위해 사용됨.
        /// 예) 구봉중학교 기준으로 Query를 한 CSV파일을 아래와 같이 수정한 것에 대해 RSTOP을 계산하는데 사용됨.
        ///     "구봉중학교 -> 여수고등학교"를 "국동주택단지 -> 여수고등학교"
        /// </summary>
        /// <param name="Route_ID">노선 ID</param>
        /// <param name="STOP_ID">기준 정류장 ID - 만약 기준점이 "구봉중학교"이면, "구봉중학교"이라는 정류장의 ID가 값이 됨.</param>
        /// <param name="CP_STOP_ID">현재 위치에 있는 정류장 ID - 해당 노선이 현재 "여수고등학교" 정류장에 있으면 "여수고등학교"이라는 정류장의 ID가 값이 됨.</param>
        /// <returns>지정한 STOP_ID와 CP_STOP_ID의 구간거리</returns>
        public String CalculateRSTOP(string Route_ID, string STOP_ID, string CP_STOP_ID)
        {
            List<KeyValuePair<Int32, string>> route;
            int i = 0, k = 0;


            using (Mutex m = new Mutex(false, "MutexName1"))
            {
                if (!BusStop_Routes.ContainsKey(Route_ID))
                {
                    if ((route = GetBusStopes_moving(Route_ID)) == null)
                    {
                        throw new ArgumentNullException("Called from Get_RSTOP_distance(), Message : Can't get Route(ROUTE_ID : " + Route_ID + ".", "None");
                        //return new KeyValuePair<Int32, string>(0, null);
                    }

                    BusStop_Routes.Add(Route_ID, route);
                }
            }

            foreach (KeyValuePair<Int32, string> keyValue in BusStop_Routes[Route_ID])
            {
                i++;
                if (keyValue.Key == ConvertStrToNaturalNumber(STOP_ID))
                {
                    break;
                }
            }


            foreach (KeyValuePair<Int32, string> keyValue in BusStop_Routes[Route_ID])
            {
                k++;
                if (keyValue.Key == ConvertStrToNaturalNumber(CP_STOP_ID))
                {
                    break;
                }
            }

            if (i < k)
            {
                foreach (KeyValuePair<Int32, string> keyValue in BusStop_Routes[Route_ID])
                {
                    Console.WriteLine("{0} : {1}", keyValue.Key, keyValue.Value);
                }

                throw new ArgumentNullException("Called from Get_RSTOP_distance(), Message : 현재 위치에 있는 정류장은 기준 정류장보다 클 수 없습니다.");
            }

            if (k > i)
                return "";

            return (i - k) + "구간전";
        }

        /// <summary>
        /// CSV파일을 수정하는 용도로 만들어졌고, 기준점을 임의로 바꾸기 위해 사용됨.
        /// </summary>
        /// <param name="Route_ID">노선 ID</param>
        /// <param name="STOP_ID">기준 정류장 ID - 만약 기준점이 "구봉중학교"이면, "구봉중학교"이라는 정류장의 ID가 값이 됨.</param>
        /// <param name="CP_STOP_ID">현재 위치에 있는 정류장 ID - 해당 노선이 현재 "여수고등학교" 정류장에 있으면 "여수고등학교"이라는 정류장의 ID가 값이 됨.</param>
        /// <returns>STOP_ID와 CP_STOP_ID의 사이에 있는 정류장들의 목록.</returns>
        public List<KeyValuePair<Int32, string>> GetBetweenStopLists(string Route_ID, string STOP_ID, string CP_STOP_ID)
        {
            List<KeyValuePair<Int32, string>> route, between_route = new List<KeyValuePair<int, string>>();
            bool isThere_STOPID = false, isThere_CP_STOPID = false;


            using (Mutex m = new Mutex(false, "MutexName1"))
            {
                if (!BusStop_Routes.ContainsKey(Route_ID))
                {
                    if ((route = GetBusStopes_moving(Route_ID)) == null)
                    {
                        throw new ArgumentNullException("Called from Get_RSTOP_distance(), Message : Can't get Route(ROUTE_ID : " + Route_ID + ".", "None");
                        //return new KeyValuePair<Int32, string>(0, null);
                    }

                    BusStop_Routes.Add(Route_ID, route);
                }
            }

            foreach (KeyValuePair<Int32, string> keyValue in BusStop_Routes[Route_ID])
            {
                if (keyValue.Key.ToString() == STOP_ID)
                    isThere_STOPID = true;

                if (isThere_STOPID)
                    between_route.Add(new KeyValuePair<int, string>(keyValue.Key, keyValue.Value));

                if (keyValue.Key.ToString() == STOP_ID)
                {
                    isThere_STOPID = true;
                    break;
                }
            }

            if (isThere_STOPID && isThere_CP_STOPID)
                return between_route;
            else
                return null;
        }

        /// <summary>
        /// MakeBusesArrivalHistory함수에서 생성한 CSV파일을 분석하여 같은 회차끼리 정렬합니다.
        /// </summary>
        /// <param name="fileName">MakeBusesArrivalHistory함수에서 생성한 CSV파일이름</param>
        /// <returns>정렬 결과를 반환(성공 : true, 실패 : false)합니다.</returns>
        public static bool BusHistory_CSVFile_sort(string fileName)
        {
            String csvDirPath;
            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists)
            {
                throw new System.ArgumentException("Called from BusHistory_CSVFile_sort(), Message : No Such File(" + fileName + ").", "directory fault");
                return false;
            }

            System.IO.StreamReader file = new System.IO.StreamReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None));


            string strLine = file.ReadLine();
            string[] column = strLine.Split(','), temp;        // Split() 메서드를 이용하여 ',' 구분하여 잘라냄
            List<string[]> row = new List<string[]>();
            int RSTOP_index = -1, ROUTE_ID_index = -1, WRITE_CODE_index = -1, RSTOP, ROUTE_ID, Line_Hash, Line_Hash_index = -1, index;
#if DEBUG1234
            string STOP_ID = "", STOP_NAME = "", ROUTE_NAME = "";
            int STOP_ID_index = -1, STOP_NAME_index = -1, ROUTE_NAME_index = -1;
#endif

            int max_Line = 1000/*500*/, i, j, k;

            bool isDoReadJump = false, isFileEnd = false;


            int line_count = 1;
            string[] temp_buf = column;
            column = new string[column.Length + 1];
            for (j = 0; j < column.Length - 1; j++)
                column[j] = temp_buf[j];
            column[j] = "Line-Hash";
            Line_Hash_index = j;

            for (i = 0; i < column.Length; i++)
            {
                if (column[i] == _getArrInfoByStopID.msgBody.ROUTE_ID)
                    ROUTE_ID_index = i;
                else if (column[i] == _getArrInfoByStopID.msgBody.RSTOP)
                    RSTOP_index = i;
                else if (column[i] == "WRITE-CODE")
                    WRITE_CODE_index = i;
                if (RSTOP_index != -1 && ROUTE_ID_index != -1 && WRITE_CODE_index != -1)
                    break;
            }

            if (RSTOP_index == -1 || ROUTE_ID_index == -1 || WRITE_CODE_index == -1)
            {
                throw new System.ArgumentException("Called from BusHistory_CSVFile_sort(), Message : Can't find these columns(RSTOP, ROUTE_ID, WRITE-CODE)", "csv file content fault");
                return false;
            }
#if DEBUG1234
            for (i = 0; i < column.Length; i++)
                if (column[i] == _getArrInfoByStopID.msgBody.STOP_ID)
                {
                    STOP_ID_index = i;
                    break;
                }
            for (i = 0; i < column.Length; i++)
                if (column[i] == _getArrInfoByStopID.msgBody.STOP_NAME)
                {
                    STOP_NAME_index = i;
                    break;
                }
            for (i = 0; i < column.Length; i++)
                if (column[i] == _getArrInfoByStopID.msgBody.ROUTE_NAME)
                {
                    ROUTE_NAME_index = i;
                    break;
                }
#endif

            strLine = file.ReadLine();
            temp = strLine.Split(',');        // Split() 메서드를 이용하여 ',' 구분하여 잘라냄

            temp_buf = temp;
            temp = new string[temp.Length + 1];
            for (j = 0; j < temp.Length - 1; j++)
                temp[j] = temp_buf[j];
            temp[j] = (++line_count).ToString();

            if (column.Length != temp.Length && temp.Length > 0)
            {
                throw new System.ArgumentException("Called from BusHistory_CSVFile_sort(), Message : Does not match the number of columns.", "csv file content fault");
                return false;
            }

            csvDirPath = "C:\\Users\\Administrator\\Documents\\"
                + String.Format("{0}\\{1}년 {2}월\\Sorted CSV Files\\", "Bus Information for Big Data", DateTime.Now.Year, DateTime.Now.Month);
            fileName = fileName.Substring(fileName.LastIndexOf('\\') + 1);
            fileName = fileName.Replace(".csv", "");
            DirectoryInfo di = new DirectoryInfo(csvDirPath);
            if (di.Exists == false)
            {
                di.Create();
            }
            using (StreamWriter outputFile = new StreamWriter(String.Format("{0}\\{1}-sort.csv", csvDirPath, fileName), false, Encoding.UTF8))
            {
                for (k = 0; k < column.Length - 1; k++) // Write.
                {
                    outputFile.Write("{0},", column[k]);
                }
                outputFile.WriteLine("{0}", column[k]);

#if DEBUG1234
                outputFile.WriteLine("{0},\t\t회차변경", strLine);
#else
                outputFile.WriteLine(strLine);
#endif
                RSTOP = ConvertStrToNaturalNumber(temp[RSTOP_index]);
                ROUTE_ID = ConvertStrToNaturalNumber(temp[ROUTE_ID_index]);
#if DEBUG1234
                if (STOP_ID_index != -1)
                    STOP_ID = temp[STOP_ID_index];

                if (STOP_NAME_index != -1)
                    STOP_NAME = temp[STOP_NAME_index];

                if (ROUTE_NAME_index != -1)
                    ROUTE_NAME = temp[ROUTE_NAME_index];
#endif
                do
                {
                    for (i = 0; i < max_Line; i++) // 파일과 메모리(List)를 병행해서 찾아보기
                    {
                        if (!isDoReadJump && !isFileEnd)
                        {
                            strLine = file.ReadLine();
                            if (strLine == null)
                            {
                                isFileEnd = true;
                                file.Close();
                                break;
                            }

                            temp = strLine.Split(',');        // Split() 메서드를 이용하여 ',' 구분하여 잘라냄
                            temp_buf = temp;
                            temp = new string[temp.Length + 1];
                            for (j = 0; j < temp.Length - 1; j++)
                                temp[j] = temp_buf[j];
                            temp[j] = (++line_count).ToString();
#if DEBUG1234

                            Console.Write("ROUTE_ID : {0}", temp[ROUTE_ID_index]);

                            if (ROUTE_NAME_index != -1)
                                Console.Write(", ROUTE_NAME : {0}", temp[ROUTE_NAME_index]);

                            if (STOP_ID_index != -1)
                                Console.Write(", STOP_ID : {0}", temp[STOP_ID_index]);

                            if (STOP_NAME_index != -1)
                                Console.Write(", STOP_NAME : {0}", temp[STOP_NAME_index]);
                            Console.Write(", Line-Hash : {0}", line_count);
                            Console.WriteLine();
#endif
                            if (column.Length != temp.Length && temp.Length > 0)
                            {
                                throw new System.ArgumentException("Called from BusHistory_CSVFile_sort(), Message : Does not match the number of columns.", "csv file content fault");
                                return false;
                            }

                            row.Add(temp);


                            isDoReadJump = false;
                        }

                        if (row.Count == 0)
                        {
                            if (!isFileEnd)
                            {
                                isDoReadJump = false;
                                continue;
                            }
                            else
                                return true;
                        }

                        if (ROUTE_ID == ConvertStrToNaturalNumber(row[row.Count - 1][ROUTE_ID_index])) // 시간순서에 따라 적히는 것을 이용함, 파일을 쓰는 방식에 종속됨(여수시에서 변경할 경우에만 영향을 받음).
                        {
                            if ((index = Define_WhichClosestRoute(RSTOP, RSTOP_index
                                , ROUTE_ID, ROUTE_ID_index
                                , ConvertStrToNaturalNumber(row[row.Count - 1][Line_Hash_index]), Line_Hash_index
                                , row)) >= 0)
                            {
                                for (k = 0; k < row[index].Length - 1; k++) // Write.
                                {
                                    outputFile.Write("{0},", row[index][k]);
                                }
#if !DEBUG1234
                                outputFile.WriteLine();
#else

                                outputFile.Write("{0}", row[index][k]);
                                outputFile.WriteLine(",\t\t파일 읽기와 List활용");
#endif

                                RSTOP = ConvertStrToNaturalNumber(row[index][RSTOP_index]);// RSTOP 업데이트
                                i = 0; // 최대로 읽을 수 있는 줄수 초기화

                                row.RemoveAt(index);

                                if (RSTOP == 0)
                                {
                                    break;
                                }



                                isDoReadJump = true;
                            }
                            else
                                isDoReadJump = false;
                        }
                        else
                            isDoReadJump = false;
                    }


                    for (i = 0; i < row.Count; i++) // List에서 찾아보기
                    {
                        if (ROUTE_ID == ConvertStrToNaturalNumber(row[i][ROUTE_ID_index])) // 시간순서에 따라 적히는 것을 이용함, 파일을 쓰는 방식에 종속됨(여수시에서 변경할 경우에만 영향을 받음).
                        {
                            if ((index = Define_WhichClosestRoute(RSTOP, RSTOP_index
                                , ROUTE_ID, ROUTE_ID_index
                                , ConvertStrToNaturalNumber(row[i][Line_Hash_index]), Line_Hash_index
                                , row)) >= 0)
                            {
                                for (k = 0; k < row[index].Length - 1; k++) // Write.
                                {
                                    outputFile.Write("{0},", row[index][k]);
                                }
#if !DEBUG1234
                                outputFile.WriteLine();
#else

                                outputFile.Write("{0}", row[index][k]);
                                outputFile.WriteLine(",\t\tList내에서 검색1");
#endif

                                RSTOP = ConvertStrToNaturalNumber(row[index][RSTOP_index]);// RSTOP 업데이트
                                i = 0; // 최대로 읽을 수 있는 줄수 초기화

                                row.RemoveAt(index);

                                if (RSTOP == 0)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    if (row.Count > 0 /*&& ((ConvertStrToNaturalNumber(row[0][WRITE_CODE_index]) % 2) == 1)*/) // 회차 변경(1. RSTOP : 0, 2. max_line줄까지 읽었으나 찾지 못한 경우 다 찾은것으로 판단하기)
                    {
                        int new_cycle = 0;
                        for (k = 0; k < row.Count; k++)
                            if (ConvertStrToNaturalNumber(row[k][WRITE_CODE_index]) % 2 == 1)
                            {
                                new_cycle = k;
                                break;
                            }
                        RSTOP = ConvertStrToNaturalNumber(row[new_cycle][RSTOP_index]);
                        ROUTE_ID = ConvertStrToNaturalNumber(row[new_cycle][ROUTE_ID_index]);
#if DEBUG1234
                        outputFile.WriteLine();

                        if (STOP_ID_index != -1)
                            STOP_ID = temp[STOP_ID_index];

                        if (STOP_NAME_index != -1)
                            STOP_NAME = temp[STOP_NAME_index];

                        if (ROUTE_NAME_index != -1)
                            ROUTE_NAME = temp[ROUTE_NAME_index];
#endif
                        for (k = 0; k < row[new_cycle].Length - 1; k++) // Write.
                        {
                            outputFile.Write("{0},", row[new_cycle][k]);
                        }
#if !DEBUG1234
                        outputFile.WriteLine();
#else

                        outputFile.Write("{0}", row[new_cycle][k]);
                        outputFile.WriteLine(",\t\t회차 변경");

#endif
                        row.RemoveAt(new_cycle);
                    }
                    isDoReadJump = true; // try..
                } while (row.Count > 0 || !isFileEnd);

            }


            return true;
        }

        /// <summary>
        /// 여러개의 CSV파일을 하나로 병합합니다.
        /// 
        /// GetFileLists함수를 사용하여 한 폴더내에 있는 모든 CSV파일을 병합할 수 있습니다.
        /// 방법은 아래와 같습니다.
        /// Merge_CSVFile(""2020-03-14, @"..\..\xml (2020-03-14)")
        /// </summary>
        /// <param name="CSV_FileName">생성할 CSV파일이름</param>
        /// <param name="fileNameLists">병합할 CSV파일이름들(경로)</param>
        public static void Merge_CSVFile(string CSV_FileName, string[] fileNameLists)
        {
            System.IO.StreamReader file;
            string strLine;

            CSV_FileName += ".csv";

            using (StreamWriter outputFile = new StreamWriter("C:\\Users\\Administrator\\Documents\\" + CSV_FileName, false, Encoding.UTF8))

            {
                file = new System.IO.StreamReader(fileNameLists[0]);
                outputFile.WriteLine(file.ReadToEnd());

                fileNameLists.CopyTo(fileNameLists, 1);
                foreach (string fileName in fileNameLists)
                {
                    FileInfo fileInfo = new FileInfo(fileName);
                    if (!fileInfo.Exists)
                    {
                        throw new System.ArgumentException("Called from Merge_CSVFile(), Message : No Such File(" + fileName + ").", "directory fault");
                    }

                    file = new System.IO.StreamReader(fileName);
                    if ((strLine = file.ReadLine()) == null) // CSV 헤더 건너뛰기.
                        throw new System.ArgumentException("Called from Merge_CSVFile(), Message : The content is empty");

                    while ((strLine = file.ReadLine()) != null)
                        outputFile.WriteLine(strLine);
                }
            }
        }

        /// <summary>
        /// 지정한 경로(폴더)에 있는 파일들에 대한 FileInfo를 반환합니다.
        /// </summary>
        /// <param name="dirPath">지정할ㄴ 경로(폴더)</param>
        /// <returns>지정한 경로(폴더)에 있는 파일들에 대한 FileInfo</returns>
        protected static List<System.IO.FileInfo> GetFileLists(String dirPath)
        {
            List<System.IO.FileInfo> FileLists = new List<System.IO.FileInfo>();

            if (System.IO.Directory.Exists(dirPath))
            {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(dirPath);

                foreach (System.IO.FileInfo item in di.GetFiles())
                {
                    FileLists.Add(item);
                }
            }
            else
                return null;

            return FileLists;
        }
    }
}
