using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BIS_Big_Data__Yeosu__4.GetXMLClass.Attributes.BusOperationInfo_ClassMembers;

namespace BIS_Big_Data__Yeosu__4.GetXMLClass.Attributes
{
    class _BusOperationInfo
    {
        private const string BusOperationInfo_URL = "http://apis.data.go.kr/4810000/busRouteInfo/";
        public _getStaionByRoute getStaionByRoute;
        public _getStaionByRouteAll getStaionByRouteAll;
        public _getRouteInfo getRouteInfo;
        public _getRouteInfoAll getRouteInfoAll;

        public _BusOperationInfo(string ServiceKey)
        {
            getStaionByRoute = new _getStaionByRoute(BusOperationInfo_URL, ServiceKey);
            getStaionByRouteAll = new _getStaionByRouteAll(BusOperationInfo_URL, ServiceKey);
            getRouteInfo = new _getRouteInfo(BusOperationInfo_URL, ServiceKey);
            getRouteInfoAll = new _getRouteInfoAll(BusOperationInfo_URL, ServiceKey);
        }
    }
}
