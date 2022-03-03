using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using TimeZoneConverter;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace RemoBatch
{
    public class Function
    {
        private static AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        private static string tableName = "remo_batch_data";
        private static HttpClient httpClient;


        public void FunctionHandler()

        {
            httpClient = new HttpClient();
            try
            {
                string remoKey = Environment.GetEnvironmentVariable("REMO_KEY");
                string headerValue = "Bearer " + remoKey;

                //室内データ
                var devicesRequest = new HttpRequestMessage(HttpMethod.Get, @"https://api.nature.global/1/devices");
                devicesRequest.Headers.Add(@"Authorization", @headerValue);
                var devicesResponse = httpClient.SendAsync(devicesRequest);
                var devicesJson = devicesResponse.Result.Content.ReadAsStringAsync().Result;
                List<Devices> devices = JsonConvert.DeserializeObject<List<Devices>>(devicesJson);

                //現在時刻(JST)
                var jstZoneInfo = TZConvert.GetTimeZoneInfo("Tokyo Standard Time");
                DateTime jst = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, jstZoneInfo).DateTime;
                string jstStr = jst.ToString("yyyy/MM/dd HH:mm:ss");

                //エアコンデータ
                var appliancesRequest = new HttpRequestMessage(HttpMethod.Get, @"https://api.nature.global/1/appliances");
                appliancesRequest.Headers.Add(@"Authorization", @headerValue);
                var appliancesResponse = httpClient.SendAsync(appliancesRequest);
                var appliancesJson = appliancesResponse.Result.Content.ReadAsStringAsync().Result;
                List<Appliances> appliances = JsonConvert.DeserializeObject<List<Appliances>>(appliancesJson);
                Settings airconSettings = appliances.Find(appliance => appliance.type == "AC").settings;

                //アメダスデータ
                //現在気温の取得には15分程度ラグがあるので、15分前の気温を取得することにする
                var before15Minutesjst = jst.AddMinutes(-15);
                var jstAmedasStr = before15Minutesjst.ToString("yyyyMMddHH");
                var jstAmedasStrMinute = before15Minutesjst.ToString("mm").Substring(0, 1);

                Amedas sagamiharaAmedasData;

                try
                {
                    //気象庁からアメダスデータを取得
                    var amedasUrl = "https://www.jma.go.jp/bosai/amedas/data/map/" + jstAmedasStr + jstAmedasStrMinute + "000.json";
                    var amedasRequest = new HttpRequestMessage(HttpMethod.Get, amedasUrl);
                    var amedasResponse = httpClient.SendAsync(amedasRequest);
                    var amedasJson = amedasResponse.Result.Content.ReadAsStringAsync().Result;
                    var amedasData = JObject.Parse(@amedasJson);
                    //アメダス 相模原観測点のデータを抽出
                    sagamiharaAmedasData = amedasData.SelectToken("$..46091").ToObject<Amedas>();

                }
                catch (Exception e)
                {
                    LambdaLogger.Log(e.Message);
                    LambdaLogger.Log(e.StackTrace);
                    sagamiharaAmedasData = null;
                }



                //DBに保存
                Table batchDb = Table.LoadTable(client, tableName);
                var data = new Document();
                
                //日時
                data["DateTime"] = jstStr;
                LambdaLogger.Log("DateTime : " + jstStr);

                //室温
                data["RoomTemperature"] = devices[0].newest_events.te.val;
                LambdaLogger.Log("RoomTemperature : " + devices[0].newest_events.te.val);

                //エアコンを使っているかどうか
                data["IsUsingAircon"] = airconSettings.button == "power-on";
                LambdaLogger.Log("IsUsingAircon : " + airconSettings.button);

                //エアコンの設定温度
                data["AirconTemperature"] = airconSettings.temp;
                LambdaLogger.Log("AirconTemperature : " + airconSettings.temp);

                //エアコンの動作モード
                data["AirconMode"] = airconSettings.mode;
                LambdaLogger.Log("AirconMode : " + airconSettings.mode);

                //外気温
                data["OutdoorTemperature"] = sagamiharaAmedasData?.temp[0];
                LambdaLogger.Log("OutdoorTemperature : " + sagamiharaAmedasData?.temp[0]);

                var result = batchDb.UpdateItemAsync(data).Result;
                

                httpClient.Dispose();

            }
            catch (Exception e)
            {
                LambdaLogger.Log(e.ToString());
                LambdaLogger.Log(e.Message);
                LambdaLogger.Log(e.StackTrace);
            }

        }
    }
}
