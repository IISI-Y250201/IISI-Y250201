```csharp
/*******************************************************************
程式代碼：icei4014b01
程式名稱：未參與網路月租費院所季結算獎勵名單作業
功能簡述：處理未參與網路月租費院所季結算獎勵名單
參    數：
參數一：季別
範例一：icei4014b01 37
讀取檔案：無
異動檔案：ICEI_ASSAY_DTL
作    者：AI Assistant
歷次修改時間：
1.2023/10/01
需求單號暨修改內容簡述：
1.將Pro*C程式轉換為C#
備    註：
********************************************************************/

using System;
using System.Data;
using System.Text;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei4014b01
{
    public class icei4014b01
    {
        /* ---------- static members ---------- */
        private static OracleConnection _oraConn =
            new OracleConnection(GetDBInfo.GetHmDBConnectString);

        private static Logger _logger =
            LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());

        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }
        private static ProList _proList = new ProList();

        /* ---------- variables ---------- */
        private static string _seasonTot = string.Empty;
        private static string _sysDate = string.Empty;

        /* ---------- Main ---------- */
        static void Main(string[] args)
        {
            try
            {
                // PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                WriteMsg("程式開始執行");

                CheckArg(args);

                _oraConn.Open();

                // 參數季別有任一筆data_type=4，不可執行
                int cnt = 0;
                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT COUNT(*)");
                    strSQL.AppendLine("  FROM ICEI_ASSAY_DTL");
                    strSQL.AppendLine(" WHERE SEASON_TOT = :seasonTot");
                    cmd.Parameters.Add(new OracleParameter("seasonTot", _seasonTot));
                    strSQL.AppendLine("   AND DATA_TYPE = '4'");

                    cmd.CommandText = strSQL.ToString();
                    cnt = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (cnt > 0)
                {
                    WriteMsg($"{_seasonTot}季已有data_type=4案件{cnt}筆");
                    _proList.exitCode = 0;
                    return;
                }

                // 參數季別未有data_type=3，不執行
                cnt = 0;
                strSQL.Clear();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT COUNT(*)");
                    strSQL.AppendLine("  FROM ICEI_ASSAY_DTL");
                    strSQL.AppendLine(" WHERE SEASON_TOT = :seasonTot");
                    cmd.Parameters.Add(new OracleParameter("seasonTot", _seasonTot));
                    strSQL.AppendLine("   AND DATA_TYPE = '3'");

                    cmd.CommandText = strSQL.ToString();
                    cnt = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (cnt == 0)
                {
                    WriteMsg($"{_seasonTot}季未有data_type=3案件");
                    _proList.exitCode = 0;
                    return;
                }

                // 刪除資料
                strSQL.Clear();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("DELETE ICEI_ASSAY_DTL");
                    strSQL.AppendLine(" WHERE SEASON_TOT = :seasonTot");
                    cmd.Parameters.Add(new OracleParameter("seasonTot", _seasonTot));
                    strSQL.AppendLine("   AND DATA_TYPE = '3'");
                    strSQL.AppendLine("   AND NET_TYPE = 'N'");

                    cmd.CommandText = strSQL.ToString();
                    int deleteCount = cmd.ExecuteNonQuery();
                    WriteMsg($"刪除icei_assay_dtl {deleteCount}筆");
                }

                // 補未參與網路月租費院所，由data_type='2'未有data_type='3'之案件，寫入data_type='3' AND net_type = N.
                strSQL.Clear();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("INSERT INTO ICEI_ASSAY_DTL (");
                    strSQL.AppendLine("       BRANCH_CODE, DATA_TYPE, HOSP_ID, SEASON_TOT, FEE_YM, HOSP_CNT_TYPE, FEE_QQ, SEASON_CNT,");
                    strSQL.AppendLine("       SEASON_S_DATE, SEASON_E_DATE, NET_TYPE, HOSP_ID_M, UPLD_BASIC_FEE, APPL_ORDER_QTY,");
                    strSQL.AppendLine("       UPLD_ORDER_QRY, UPLD_RATE, REPORT_QTY1, REPORT_U_P1, REPORT_QTY2, REPORT_U_P2, FEE_RATE,");
                    strSQL.AppendLine("       MDS_QTY, MDS_U_P, PAY_AMT, CREATE_DATE, TXT_DATE, CHT_FEE, HOSP_DATA_TYPE, APPL_TYPE,");
                    strSQL.AppendLine("       APPL_DATE, ACPT_DATE, OIPD_TYPE, KEEN_QTY, KEEN_U_P, REF_IN_QTY, REF_IN_U_P, REF_OUT_QTY,");
                    strSQL.AppendLine("       REF_OUT_U_P, HOME_IC_QTY, HOME_OP_QTY, HOME_U_P, HOME_QTY, SPEC_QTY, SPEC_U_P, ASSAY_STATUS,");
                    strSQL.AppendLine("       ASSAY_QTY, ASSAY_U_P, FEE_RATE_C, REPORT_QTY3, ASSAY_QTY2, ASSAY_U_P2, CTMRI_QTY1,");
                    strSQL.AppendLine("       CTMRI_U_P1, CTMRI_QTY2, CTMRI_U_P2, CTMRI_QTY3, CTMRI_U_P3, ASSAY_3_QTY, ASSAY_3_QTY2,");
                    strSQL.AppendLine("       CTMRI_3_QTY1, CTMRI_3_QTY2, CTMRI_3_QTY3, PRE_REWARD, APPL_REWARD, HOME_REWARD, FUNC_REWARD,");
                    strSQL.AppendLine("       ORIG_ASSAY_QTY1, ORIG_ASSAY_QTY2, OTH_ASSAY_QTY1, OTH_ASSAY_QTY2, ORIG_ASSAY_3_QTY1,");
                    strSQL.AppendLine("       ORIG_ASSAY_3_QTY2, OTH_ASSAY_3_QTY1, OTH_ASSAY_3_QTY2, A_PAY_AMT, I_PAY_AMT )");
                    strSQL.AppendLine("SELECT BRANCH_CODE, '3' DATA_TYPE, HOSP_ID, SEASON_TOT, FEE_YM, HOSP_CNT_TYPE, FEE_QQ, SEASON_CNT,");
                    strSQL.AppendLine("       SEASON_S_DATE, SEASON_E_DATE, 'N' NET_TYPE, HOSP_ID_M, UPLD_BASIC_FEE, APPL_ORDER_QTY,");
                    strSQL.AppendLine("       UPLD_ORDER_QRY, UPLD_RATE, REPORT_QTY1, REPORT_U_P1, REPORT_QTY2, REPORT_U_P2, FEE_RATE,");
                    strSQL.AppendLine("       MDS_QTY, MDS_U_P, PAY_AMT, CREATE_DATE, TXT_DATE, CHT_FEE, HOSP_DATA_TYPE, APPL_TYPE,");
                    strSQL.AppendLine("       APPL_DATE, ACPT_DATE, OIPD_TYPE, KEEN_QTY, KEEN_U_P, REF_IN_QTY, REF_IN_U_P, REF_OUT_QTY,");
                    strSQL.AppendLine("       REF_OUT_U_P, HOME_IC_QTY, HOME_OP_QTY, HOME_U_P, HOME_QTY, SPEC_QTY, SPEC_U_P, ASSAY_STATUS,");
                    strSQL.AppendLine("       ASSAY_QTY, ASSAY_U_P, FEE_RATE_C, REPORT_QTY3, ASSAY_QTY2, ASSAY_U_P2, CTMRI_QTY1,");
                    strSQL.AppendLine("       CTMRI_U_P1, CTMRI_QTY2, CTMRI_U_P2, CTMRI_QTY3, CTMRI_U_P3, ASSAY_3_QTY, ASSAY_3_QTY2,");
                    strSQL.AppendLine("       CTMRI_3_QTY1, CTMRI_3_QTY2, CTMRI_3_QTY3, PRE_REWARD, APPL_REWARD, HOME_REWARD, FUNC_REWARD,");
                    strSQL.AppendLine("       ORIG_ASSAY_QTY1, ORIG_ASSAY_QTY2, OTH_ASSAY_QTY1, OTH_ASSAY_QTY2, ORIG_ASSAY_3_QTY1,");
                    strSQL.AppendLine("       ORIG_ASSAY_3_QTY2, OTH_ASSAY_3_QTY1, OTH_ASSAY_3_QTY2, A_PAY_AMT, I_PAY_AMT");
                    strSQL.AppendLine("  FROM ICEI_ASSAY_DTL");
                    strSQL.AppendLine(" WHERE SEASON_TOT = :seasonTot");
                    cmd.Parameters.Add(new OracleParameter("seasonTot", _seasonTot));
                    strSQL.AppendLine("   AND DATA_TYPE = '2'");
                    strSQL.AppendLine("   AND (SEASON_TOT, HOSP_ID) NOT IN");
                    strSQL.AppendLine("       (SELECT SEASON_TOT, HOSP_ID");
                    strSQL.AppendLine("          FROM ICEI_ASSAY_DTL");
                    strSQL.AppendLine("         WHERE SEASON_TOT = :seasonTot2");
                    cmd.Parameters.Add(new OracleParameter("seasonTot2", _seasonTot));
                    strSQL.AppendLine("           AND DATA_TYPE = '3')");

                    cmd.CommandText = strSQL.ToString();
                    int insertCount = cmd.ExecuteNonQuery();
                    WriteMsg($"新增 icei_assay_dtl {insertCount}筆");
                }

                WriteMsg("程式執行完成");

                _proList.exitCode = 0;
                _proList.message = $"\n程式 icei4014b01 結束\n";
                Console.WriteLine(_proList.message);
            }
            catch (OracleException ex)
            {
                _proList.exitCode = 200;
                _proList.message = ex.Message;
                Console.WriteLine(ex.ToString());
                _logger.Error(ex.ToString());
            }
            catch (Exception ex)
            {
                _proList.message = ex.Message;
                Console.WriteLine(ex.ToString());
                _logger.Error(ex.ToString());
            }
            finally
            {
                if (_oraConn.State == ConnectionState.Open)
                {
                    _oraConn.Close();
                }
                
                // PXX_exit_process(rtn_code, 0, msg);
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        /* ---------- parameter check ---------- */
        // Original: check_arg()
        private static void CheckArg(string[] args)
        {
            if (args.Length != 1)
            {
                _proList.exitCode = 1;
                _proList.message = "參數個數不符";

                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"參數種類：   程式代號  季別");
                Console.WriteLine($"範例    ： {prog}  37");
                
                _logger.Error(_proList.message);
                throw new ArgumentException(_proList.message);
            }

            _seasonTot = args[0];
            WriteMsg($"參數：{_seasonTot}");

            _logger.Info($"Args → {string.Join(",", args)}");
        }

        #region Helper Methods
        // Original: get_sysdate()
        private static void GetSysDate()
        {
            _sysDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }

        // Original: write_msg()
        private static void WriteMsg(string format, params object[] args)
        {
            string message = string.Format(format, args);
            GetSysDate();
            string formattedMessage = $"<{_sysDate}>\n{message}";
            
            Console.WriteLine(formattedMessage);
            _logger.Info(formattedMessage);
        }
        #endregion
    }
}
```