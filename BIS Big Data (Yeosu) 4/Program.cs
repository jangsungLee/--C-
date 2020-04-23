// #define MAIN_DEBUG

using BIS_Big_Data__Yeosu__4.GetXMLClass.Attributes;
using BIS_Big_Data__Yeosu__4.GetXMLClass.Attributes.BusOperationInfo_ClassMembers;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
namespace BIS_Big_Data__Yeosu__4
{
    class Program
    {
        static void Main(string[] args)
        {
             BusInfo bis = new BusInfo("발급받은 ServiceKey"); // 랩실

            //checkIntegrity("325000846 도착정보 (STOP_ID - 325000846, 2020-3-23).csv");

            bis.startTime = new Time(4, 00); // 시작시간 수정
            bis.total_available_Connection_count = 20000; // 1일당 최대 연결할 수 있는 횟수 수정


            Task taskA = bis.MakeTask_ArrivalHistory("325000846"); // 이순신광장 도착정보 Task 생성
            Task taskB = bis.MakeTask_ArrivalHistory("325000843"); // 한화생명 도착정보 Task 생성

            taskA.Start();
            taskB.Start();
            
            // bis.Stop_TaskArrivalHistory(); // 시작한 Task들 동작중지 - real_intervalTime * TaskCount만큼 대기한 후에 연결하는 구조때문에, 즉시 중단되지 않고 일정시간 대기후에 정지함.
            
            taskA.Wait();
            taskB.Wait();
        }

        static void MakeHistoryFromXML(String dirPath, ref BusInfo bis)
        {
            Dictionary<string, List<XmlNode>> rstop = new Dictionary<string, List<XmlNode>>();
            List<System.IO.FileInfo> fileInfos = GetFileLists((dirPath = "XML Bus Information for Big Data\\" + dirPath));
            StreamReader reader;
            String XML;

            foreach (System.IO.FileInfo fileInfo in fileInfos)
            {
                reader = new StreamReader(fileInfo.FullName);
                XML = reader.ReadToEnd();

                if (!string.IsNullOrEmpty(XML))
                {
                    bis.MakeBusesArrivalHistory(XML, ref rstop);
                }
                else
                {
                    Console.WriteLine("XML 내용이 비었습니다.");
                    Thread.Sleep(500);
                }
            }
        }

        static void getSTOPs(ref BusInfo bis, String ROUTE_ID)
        {
            List<KeyValuePair<Int32, string>> a = bis.GetBusStopes_moving(ROUTE_ID);
            using (StreamWriter outputFile = new StreamWriter(String.Format("{0} 경유 정류장.csv", ROUTE_ID), false/*no append*/, Encoding.UTF8))
            {
                outputFile.WriteLine("STOP_ID,STOP_NAME");


                foreach (KeyValuePair<Int32, string> keyValuePair in a)
                {
                    outputFile.WriteLine("{0},{1}", keyValuePair.Key, keyValuePair.Value);
                }
            }
        }

        static void checkPosition(String ROUTE_ID, String STOP_ID)
        {
            HttpWebRequest request1 = (HttpWebRequest)WebRequest.Create("http://apis.data.go.kr/4810000/busposinfo/getBusPosByRtid?serviceKey=qZBKiU5x1LgFz%2BIMpFbjQux7baXfXo7CIOsH6gmAxk3RA5%2FW8ueXezWFlmih6R7Rd5pIP4%2BqOvrwdcHctA4MiA%3D%3D&pageNo=1&numOfRows=9999&busRouteId=" + ROUTE_ID)
                , request2 = (HttpWebRequest)WebRequest.Create("http://apis.data.go.kr/4810000/arrive/getArrInfoByStopID?serviceKey=URJI2AYbycz5g4cPenp3in%2FfxWydYfh52%2BeehYRC5Op3JSvUcKF3aXodUZYPLkR1xu6Vr%2F13Af6S0xXeYYv5CA%3D%3D&pageNo=1&numOfRows=9999&busStopID=" + STOP_ID);
            request1.Method = "GET";
            request2.Method = "GET";

            HttpWebResponse response1 = request1.GetResponse() as HttpWebResponse, response2 = request2.GetResponse() as HttpWebResponse;

            using (StreamWriter outputFile = new StreamWriter("버스 현재 위치.xml", false/*no append*/, Encoding.UTF8))
            {
                StreamReader reader = new StreamReader(response1.GetResponseStream());
                outputFile.WriteLine(reader.ReadToEnd());
            }
            using (StreamWriter outputFile = new StreamWriter("정류장 도착정보.xml", false/*no append*/, Encoding.UTF8))
            {
                StreamReader reader = new StreamReader(response2.GetResponseStream());
                outputFile.WriteLine(reader.ReadToEnd());
            }
        }

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

        static bool checkIntegrity(String fileName)
        {
            fileName = String.Format("{0}\\{1}년 {2}월\\Sorted CSV Files\\{0}", "Bus Information for Big Data", DateTime.Now.Year, DateTime.Now.Month, fileName);
            FileInfo fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists)
            {
                throw new System.ArgumentException("Called from checkIntegrity(), Message : No Such File(" + fileName + ").", "directory fault");
                return false;
            }

            System.IO.StreamReader file = new System.IO.StreamReader(fileName);

            string strLine = file.ReadLine();
            string[] column = strLine.Split(','), temp;        // Split() 메서드를 이용하여 ',' 구분하여 잘라냄

            int i;
            int ROUTE_ID = -1, RSTOP = -1, prev_RSTOP = -1, ROUTE_ID_index = -1, RSTOP_index = -1, Line_Hash_index = -1;

            List<int> Line_Hash = new List<int>();

            int ROUTE_NAME, ROUTE_NAME_index = -1;

            for (i = 0; i < column.Length; i++)
            {
                if (column[i] == _getArrInfoByStopID.msgBody.ROUTE_ID)
                    ROUTE_ID_index = i;
                else if (column[i] == _getArrInfoByStopID.msgBody.RSTOP)
                    RSTOP_index = i;
                else if (column[i] == "Line-Hash")
                    Line_Hash_index = i;
                if (RSTOP_index != -1 && ROUTE_ID_index != -1 && Line_Hash_index != -1)
                    break;
            }
            if (RSTOP_index == -1 || ROUTE_ID_index == -1 || Line_Hash_index == -1)
            {
                throw new System.ArgumentException("Called from BusHistory_CSVFile_sort(), Message : Can't find these columns(RSTOP, ROUTE_ID, Line-Hash)", "csv file content fault");
                return false;
            }

            while ((strLine = file.ReadLine()) != null)
            {
                temp = strLine.Split(',');
                if (temp.Length < 2) // 회차변경
                {
                    ROUTE_ID = BusInfo.ConvertStrToNaturalNumber(temp[ROUTE_ID_index]);
                    prev_RSTOP = -1;
                    continue;
                }

                if (prev_RSTOP == -1)
                {

                }
                else
                {
                    RSTOP = BusInfo.ConvertStrToNaturalNumber(temp[RSTOP_index]);
                    if (prev_RSTOP - RSTOP != 1)
                    {
                        //throw new Exception("RSTOP간격이 1이 아닙니다 ("+strLine+ ").");
                        Console.WriteLine("RSTOP간격이 1이 아닙니다 (" + strLine + ").");
                    }
                }

                if (Line_Hash.Contains(BusInfo.ConvertStrToNaturalNumber(temp[Line_Hash_index])))
                    throw new Exception("중복된 Line Hash값이 있습니다.");
                else
                    Line_Hash.Add(BusInfo.ConvertStrToNaturalNumber(temp[Line_Hash_index]));


                prev_RSTOP = BusInfo.ConvertStrToNaturalNumber(temp[RSTOP_index]);
            }

            return true;
        }

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
