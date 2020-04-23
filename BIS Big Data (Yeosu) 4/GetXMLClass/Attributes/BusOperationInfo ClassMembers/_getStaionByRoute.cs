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
    class _getStaionByRoute
    {
        private string _url_of_Base;
        public string url_of_Base
        {
            get
            {
                return _url_of_Base;
            }
        }
        public _getStaionByRoute(string BusOperationInfo_URL, string _ServiceKey)
        {
            _url_of_Base = BusOperationInfo_URL + "getStaionByRoute" +
                XML_URL_Common_Attributes.serviceKey + _ServiceKey +
                XML_URL_Common_Attributes.numOfRows +
                XML_URL_Common_Attributes.pageNo + "1"
                + "&busRouteId=" /* + "325000100" */;
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
            static public string LAT = "LAT";
            static public string LNG = "LNG";
            static public string ROUTE_ID = "ROUTE_ID";
            static public string ROUTE_NAME = "ROUTE_NAME";
            static public string SECT_ACC_DISTANCE = "SECT_ACC_DISTANCE";
            static public string SERVICE_ID = "SERVICE_ID";
            static public string STOP_ID = "STOP_ID";
            static public string STOP_NAME = "STOP_NAME";
            static public string STOP_ORD = "STOP_ORD";
            static public string TOTAL_SECT_DISTANCE = "TOTAL_SECT_DISTANCE";
        }
    }
}
