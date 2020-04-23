using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIS_Big_Data__Yeosu__4.GetXMLClass.Attributes.BusOperationInfo_ClassMembers
{
    /// <summary>
    /// 전라남도 여수시_버스정보>버스운행정보 조회>정류소ID 별 버스 도착예정정보 조회 서비스
    /// 
    /// "class msgHeader"와 "class msgBody"는 응답 메세지의 XML속성을 담고 있음
    /// </summary>
    class _getRouteInfoAll
    {
        private string _url;
        public string url
        {
            get
            {
                return _url;
            }
        }
        public _getRouteInfoAll(string BusOperationInfo_URL, string _ServiceKey)
        {
            _url = BusOperationInfo_URL + "getRouteInfoAll" +
                XML_URL_Common_Attributes.serviceKey + _ServiceKey +
                XML_URL_Common_Attributes.numOfRows +
                XML_URL_Common_Attributes.pageNo + "1";
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
            static public string ST_STOP_ID = "ST_STOP_ID";
            static public string ED_STOP_ID = "ED_STOP_ID";
            static public string TURN_USEFLAG = "TURN_USEFLAG";
            static public string FST_TIME = "FST_TIME";
            static public string LST_TIME = "LST_TIME";
            static public string ROUTE_DIRECTION = "ROUTE_DIRECTION";
            static public string TURN_ORD = "TURN_ORD";
        }
    }
}
