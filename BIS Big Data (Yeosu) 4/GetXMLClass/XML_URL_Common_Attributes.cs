using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIS_Big_Data__Yeosu__4
{
    /// <summary>
    /// XML을 받기 위해 URL에 공통적으로 들어가는 Attribute
    /// </summary>
    class XML_URL_Common_Attributes
    {

        /// <summary>
        /// 응답 메세지에서 표시할 수 있는 최대 줄수 지정
        /// </summary>
        public const int Max_numOfRows = 9999;

        static public string serviceKey = "?ServiceKey=";
        static public string numOfRows = "&numOfRows=" + Max_numOfRows.ToString();
        static public string pageNo = "&pageNo=";
    }
}
