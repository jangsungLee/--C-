using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIS_Big_Data__Yeosu__4.GetXMLClass.Attributes
{
    /// <summary>
    /// 전라남도 여수시_버스정보>정류장 버스 도착 정보 조회>정류소ID 별 버스 도착예정정보 조회 서비스
    /// 
    /// "class msgHeader"와 "class msgBody"는 응답 메세지의 XML속성을 담고 있음
    /// </summary>
    class _getArrInfoByStopID

    {
        private string _url_of_Base;
        public string url_of_Base
        {
            get
            {
                return _url_of_Base;
            }
        }
        public _getArrInfoByStopID(string _ServiceKey)
        {
            _url_of_Base = "http://apis.data.go.kr/4810000/arrive/getArrInfoByStopID" +
               XML_URL_Common_Attributes.serviceKey + _ServiceKey +
               XML_URL_Common_Attributes.numOfRows +
               XML_URL_Common_Attributes.pageNo + "1" +
               "&busStopID=" + "";
        }

        static public class msgHeader
        {
            static public string resultCode = "resultCode";
            static public string resultMsg = "resultMsg";
            static public string totalCount = "totalCount";
            static public string numOfRows = "numOfRows";
            static public string pageNo = "pageNo";
        }

        /// <summary>
        /// XML 응답 메세지의 itemList의 속성
        /// </summary>
        static public class msgBody
        {
            static public string ROUTE_ID = "ROUTE_ID";
            static public string ROUTE_NAME = "ROUTE_NAME";
            static public string ROUTE_SUBID = "ROUTE_SUBID";
            static public string ROUTE_TYPE = "ROUTE_TYPE";
            static public string ROUTE_DIRECTION = "ROUTE_DIRECTION";
            static public string ROUTE_EXPLAIN = "ROUTE_EXPLAIN";
            static public string STOP_ID = "STOP_ID";
            static public string STOP_NAME = "STOP_NAME";
            static public string SERVICE_ID = "SERVICE_ID";
            static public string ST_STOP_NAME = "ST_STOP_NAME";
            static public string ED_STOP_NAME = "ED_STOP_NAME";
            static public string VEH_ID = "VEH_ID";
            static public string PROVIDE_TYPE = "PROVIDE_TYPE";
            static public string RSTOP = "RSTOP";
            static public string LAST_STOP_NAME = "LAST_STOP_NAME";
            static public string START_TIME = "START_TIME";
            static public string END_TIME = "END_TIME";
            static public string ALLOC_TIME = "ALLOC_TIME";
            static public string TURN_USEFLAG = "TURN_USEFLAG";
        }
    }
}
