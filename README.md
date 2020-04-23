# 여수시 버스도착 정보 C#    
이 라이브러리는 공공데이터포털 API를 활용합니다.
어느정도 배포용으로 생각하고 작성은 했지만, 몇몇부분에서는 다른사람에게는 불필요한 작업이 있어서 시간내서 정리해야될 부분이 있습니다.   
수정할지 미지수이지만, 크게 영향을 받지는 않을 것 입니다.

### 이 라이브러리를 사용하기 위해서 신청해야 할 API
"정류장 버스 도착 정보 조회"와 "버스운행정보 조회 "가 승인되어 있어야함.
(신청후 곧바로 승인표시가 뜨기는 하지만, 테스트 했을때는 서버에서 "IP등록" 그리고 "ACCESS DENY해제"가 완벽하게 되기전까지 15분~30분 정도 소요되기때문에,신청후에 직전에는 어느정도 기다린 후에 해야함)   
   
## 예제1   
[Program.cs참조](https://github.com/jangsungLee/C-Yeosu-Bus-Info-Library/blob/master/BIS%20Big%20Data%20(Yeosu)%204/Program.cs)   
   


## 기타
1. 보통 특정요일(테스트 했을 때는 주로 월~화요일에 발생함)에 XML을 받아오지 못하는 경우가 있는데, 코드자체에는 문제가 없고 서버측에 문제가 있는것으로 판단됨. 많은 시간을 가지고 테스트와 수정을 반복했었기 때문에 신뢰할 수 있는 정보입니다.
