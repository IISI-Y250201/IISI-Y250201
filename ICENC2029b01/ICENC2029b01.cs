/*******************************************************************
程式代碼：icei2029b01
程式名稱：24小時上傳註記勾稽作業
功能簡述：更新24小時上傳註記及重要檢驗結果項目標記
參    數：
參數一：程式代號 執行類別 費用年月 醫事機構代碼(選項) 有效迄日 更新註記 分區別(選項)
範例一：icei2029b01 0 20190101 "" 
範例二：icei2029b01 1 20190101 ALL
範例三：icei2029b01 2 20190101 "" 20190101 A
讀取檔案：無
異動檔案：無
作    者：系統轉換
歷次修改時間：
1.2023/01/01
需求單號暨修改內容簡述：
1.Pro*C轉C#
備    註：
********************************************************************/

using NLog;
using Oracle.ManagedDataAccess.Client;
using ICENC2029b01.Services;
using ICENC2029b01.Exceptions;
using ICENC2029b01.Repository;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;

namespace ICENC2029b01
{
    public class ICENC2029b01
    {
        private static Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());

        #region 三代新增 必寫
        // 每個要使用OpenTelemetry的.cs都要寫這段程式
        // Source名稱需與.AddSource()一致才會觸發追蹤來源，在ENVCC0001N01.OpenTelemetrySetup.cs設定
        private static readonly ActivitySource _activitySource = new ActivitySource("OpenTelemetrySource");
        #endregion

        /// <summary>
        /// 程式相關
        /// </summary>
        private class ProList
        {
            public ProList() { }
            /// <summary>
            /// 程式離開代號(自訂義)
            /// </summary>
            public int exitCode = -999;
            /// <summary>
            /// 程式離開時的訊息(自訂義)
            /// </summary>
            public string message = string.Empty;
        }
        private static ProList _proList = new ProList();

        /// <summary>
        /// 主程式
        /// </summary>
        /// <param name="args">傳入程式之參數</param>
        static void Main(string[] args)
        {
#if DEBUG
            // 測試參數
            //args = new[] { "202501", "202503" };
#endif

            #region 三代新增 必寫

            using var activity = _activitySource.StartActivity("Main"); // 需要執行StartActivity啟動追蹤

            DateTime timeStart = DateTime.Now;
            ScopeContext.PushProperty("RunCaseStart", timeStart.ToString("yyyy/MM/dd HH:mm:ss ffffff"));
            #endregion

            // ========== 以ihah1052r01作為修改範例 ==========
            try
            {
                activity?.SetTag("msg", "Start RunBatchJob");
                var batchService = new MainService();
                _proList.exitCode = batchService.RunBatchJob(args);
                if (_proList.exitCode != 0)
                {
                    throw new Exception();
                }
                _proList.message = $"產生{AppDomain.CurrentDomain.FriendlyName} ICENC2029b01 正常結束";
            }
            catch (OracleException ex)
            {
                _proList.exitCode = 200;
                string message = _proList.message;
                if (string.IsNullOrEmpty(_proList.message))
                {
                    message = ex.ToString();
                    _proList.message = ex.Message;
                }
                Console.WriteLine(message);
                Console.WriteLine(ex.Message + ex.StackTrace ?? "".ToString());
                _logger.Error(message);
                _logger.Error(ex.Message + ex.StackTrace ?? "".ToString());
                activity?.SetStatus(ActivityStatusCode.Error);
            }
            #region 三代新增 必寫
            catch (CustomException ex)
            {
                // 自訂義Exception取得Service層的ExitCode
                _proList.exitCode = ex.ExitCode;
                _proList.message = ex.Message;
                Console.WriteLine(_proList.message);
                _logger.Error(_proList.message);
                activity?.SetStatus(ActivityStatusCode.Error);
            }
            #endregion
            catch (Exception ex)
            {
                _proList.message = ex.Message;
                Console.WriteLine(ex.ToString());
                _logger.Error(ex.ToString());
                activity?.SetStatus(ActivityStatusCode.Error);
            }
            finally
            {
                #region 三代新增 必寫
                DateTime timeEnd = DateTime.Now;
                ScopeContext.PushProperty("RunCaseEnd", timeEnd.ToString("yyyy/MM/dd HH:mm:ss ffffff"));
                TimeSpan diff = timeEnd - timeStart;
                ScopeContext.PushProperty("RunTimeTotal", $"{diff.TotalSeconds}");

                activity?.SetTag("ExitCode", _proList.exitCode);
                activity?.SetTag("ExitMessage", _proList.message);
                activity?.Dispose();
                Thread.Sleep(5000);
                #endregion

            }
        }
    }
}