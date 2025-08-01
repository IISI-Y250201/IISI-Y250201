```csharp
/*******************************************************************
程式代碼：icei4010b01
程式名稱：有申報應上傳行政審查之檢驗(查)結果及醫療檢查影像上傳數統計作業
功能簡述：
(1) 檢驗(查)結果資料統計,依現行獎勵金結算架構統計,可參考icei2052b01
    A. 寫入ICEI_ASSAY_DTL_STS
       彙整ICEI_3060_PBA_ORD之1-自行即時、 2-自行三日、 3-交付即時、 4-交付三日、 5-自行逾三日、 6-交付逾三日
     上傳數至ICEI_ASSAY_DTL_STS
    B. 寫入ICEI_ASSAY_DLX_MST
(2) 醫療檢查影像資料統計寫入ICEI_IAV_DTL_STS
(3) 可輸入院所參數只彙整一家院所資料。
參    數：
參數一：分區別(必填) 費用年月(YYYYMMDD)(選項) 醫事機構代碼(選項) truncate(選項)
範例一：icei4010b01 5 20220901 1137010024 (表示該分區該費用年月單一院所重新統計)
讀取檔案：
異動檔案：
作    者：
歷次修改時間：
1.
需求單號暨修改內容簡述：
1.
備    註：
********************************************************************/

using System;
using System.Data;
using System.Text;
using System.IO;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei4010b01
{
    public class icei4010b01
    {
        #region Static Members
        private static OracleConnection _oraConn = new OracleConnection(GetDBInfo.GetHmDBConnectString);
        private static Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());
        private static ProList _proList = new ProList();

        // Parameters
        private static string _inputBranchCode = string.Empty;
        private static string _inputFeeYm = string.Empty;
        private static string _inputHospId = string.Empty;
        private static int _truncTable = 0;
        private static string _startDate = string.Empty;
        private static string _wkConvIdc = "encrypt_aes(decrypt_aes";
        #endregion

        #region Structs
        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }

        private class SQL100
        {
            public string branchCode { get; set; } = string.Empty;
            public string hospId { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
        }

        private class SQL200
        {
            public int seasonTot { get; set; }
            public string branchCode { get; set; } = string.Empty;
            public string hospId { get; set; } = string.Empty;
            public string hospCntType { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
            public string stsType { get; set; } = string.Empty;
            public string orderCode { get; set; } = string.Empty;
            public string oipdType { get; set; } = string.Empty;
            public string reportType { get; set; } = string.Empty;
            public string mark135 { get; set; } = string.Empty;
            public int applQty { get; set; }
        }

        private class SQL300
        {
            public int seasonTot { get; set; }
            public string branchCode { get; set; } = string.Empty;
            public string hospId { get; set; } = string.Empty;
            public string hospCntType { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
            public string stsType { get; set; } = string.Empty;
            public string orderCode { get; set; } = string.Empty;
            public string oipdType { get; set; } = string.Empty;
            public string reportType { get; set; } = string.Empty;
            public string mark135 { get; set; } = string.Empty;
            public int applQty { get; set; }
            public string hospDataType { get; set; } = string.Empty;
            public string mMark { get; set; } = string.Empty;
        }
        #endregion

        #region Constants
        private const int PB0_FOUND = 0;
        private const int PB0_NOT_FOUND = 1;
        #endregion

        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                CheckArg(args);

                _oraConn.Open();

                string wkBranchCode = string.Empty;
                string wkHospId = string.Empty;
                string wkFeeYm = string.Empty;
                string wkHospIdM = string.Empty;
                string wkRealHospCntType = string.Empty;
                string wkIndexType = "1"; // 門診 即時上傳, 111/12/12 本欄位不使用
                int wkSeasonTot = 0;

                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT BRANCH_CODE, HOSP_ID, TO_CHAR(FEE_YM, 'YYYYMMDD') FEE_YM,");
                    strSQL.AppendLine("       (to_char(FEE_YM,'yyyy') - '2014') * 4 + to_char(FEE_YM,'Q')");
                    strSQL.AppendLine("  FROM ICEI_3060_PBA_CTL");
                    strSQL.AppendLine(" WHERE BRANCH_CODE = :inputBranchCode");
                    cmd.Parameters.Add(new OracleParameter("inputBranchCode", _inputBranchCode));
                    strSQL.AppendLine("   AND FEE_YM      = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                    cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                    strSQL.AppendLine("   AND HOSP_ID     = DECODE(:inputHospId,'', HOSP_ID, 'ALL', HOSP_ID, :inputHospId)");
                    cmd.Parameters.Add(new OracleParameter("inputHospId", _inputHospId));
                    strSQL.AppendLine("GROUP BY BRANCH_CODE, HOSP_ID, FEE_YM, (to_char(FEE_YM,'yyyy') - '2014') * 4 + to_char(FEE_YM,'Q')");

                    cmd.CommandText = strSQL.ToString();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wkBranchCode = reader["BRANCH_CODE"].ToString();
                            wkHospId = reader["HOSP_ID"].ToString();
                            wkFeeYm = reader["FEE_YM"].ToString();
                            wkSeasonTot = Convert.ToInt32(reader[3]);

                            // Get hosp_cnt_type
                            using (OracleCommand hospCmd = _oraConn.CreateCommand())
                            {
                                hospCmd.CommandText = "SELECT hosp_cnt_type FROM mhat_hospbsc WHERE hosp_id = :hospId";
                                hospCmd.Parameters.Add(new OracleParameter("hospId", wkHospId));
                                wkRealHospCntType = hospCmd.ExecuteScalar()?.ToString() ?? string.Empty;
                            }

                            // 統計邏輯由此開始
                            InsAssayDtlSts(wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot);
                            InsOpMst("ICEI_ASSAY_DL1_MST", "1", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);
                            InsOphpMst("ICEI_ASSAY_DL2_MST", "2", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);
                            InsHpMst("ICEI_ASSAY_DL5_MST", "5", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);
                            InsOpMst("ICEI_ASSAY_DL8_MST", "6", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);
                            InsHpMst("ICEI_ASSAY_DL9_MST", "7", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);
                            InsOpRMst("ICEI_ASSAY_DLA_MST", "8", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);
                            InsHpRMst("ICEI_ASSAY_DLA_MST", "8", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);
                            InsOpRMst("ICEI_ASSAY_DLB_MST", "9", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);
                            InsHpRMst("ICEI_ASSAY_DLB_MST", "9", wkBranchCode, wkHospId, wkFeeYm, wkSeasonTot, wkRealHospCntType);

                            ExecuteNonQuery("COMMIT");
                        }
                    }
                }

                Console.WriteLine("正常結束");
                _proList.exitCode = 0;
                _proList.message = "正常結束";
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
                
                // Original: PXX_exit_process
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        #region Methods
        // Original: check_arg()
        private static void CheckArg(string[] args)
        {
            if (args.Length < 2 || args.Length > 4)
            {
                string usage = $"\n用法：{AppDomain.CurrentDomain.FriendlyName} 分區別(必填) 費用年月(YYYYMMDD)(選項) 醫事機構代碼(選項) truncate(選項)" +
                              $"\n範例：{AppDomain.CurrentDomain.FriendlyName} 5 20220901 1137010024 (表示該分區該費用年月單一院所重新統計)" +
                              $"\n範例：{AppDomain.CurrentDomain.FriendlyName} 5 20220901 ALL (表示該分區該費用年月全部重新統計)" +
                              $"\n範例：{AppDomain.CurrentDomain.FriendlyName} 5 20220901 (表示該分區該費用年月接續上次執行結果繼續統計,即只有PROC_STATUS IS NULL才作統計)";

                _proList.exitCode = 10;
                _proList.message = "參數個數不符";
                Console.WriteLine(usage);
                _logger.Error(usage);
                throw new ArgumentException("參數個數不符");
            }

            _inputBranchCode = args[0];
            
            if (args.Length >= 2)
            {
                _inputFeeYm = args[1];
            }
            
            if (args.Length >= 3)
            {
                _inputHospId = args[2];
            }
            
            if (args.Length >= 4)
            {
                if (args[3].ToUpper() == "Y")
                {
                    _truncTable = 1;
                }
                else
                {
                    _truncTable = 0;
                }
            }

            _logger.Info($"Parameters: inputBranchCode={_inputBranchCode}, inputFeeYm={_inputFeeYm}, inputHospId={_inputHospId}, truncTable={_truncTable}");
        }

        // Original: set_exe_ctrl
        private static void SetExeCtrl(int procMark, string memo)
        {
            int monsDiff = 0;
            int today = 0;
            string userId = string.Empty;
            string apArg = string.Empty;
            string truncTable = _truncTable == 1 ? "Y" : "N";

            if (string.IsNullOrEmpty(_startDate))
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT CASE WHEN :inputFeeYm = TO_CHAR(ADD_MONTHS(sysdate,-1),'yyyymm')||'01' THEN 1
                                    WHEN :inputFeeYm = TO_CHAR(ADD_MONTHS(sysdate,-2),'yyyymm')||'01' THEN 2
                                    WHEN :inputFeeYm = TO_CHAR(ADD_MONTHS(sysdate,-3),'yyyymm')||'01' THEN 3
                                    ELSE 4 END,
                               TRUNC(TO_CHAR(SYSDATE,'DD'))
                          FROM DUAL";
                    cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            monsDiff = Convert.ToInt32(reader[0]);
                            today = Convert.ToInt32(reader[1]);
                        }
                    }
                }

                // 執行參數月份與系統月相差1個月時
                if (monsDiff == 1)
                {
                    // 若今日是5~9日時，固定start_date為當月5日
                    if (today >= 5 && today <= 9)
                    {
                        using (OracleCommand cmd = _oraConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYYMM') || '05' FROM DUAL";
                            _startDate = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                        }
                    }
                    // 若今日是10~14日時，固定start_date為當月10日
                    else if (today >= 10 && today <= 14)
                    {
                        using (OracleCommand cmd = _oraConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYYMM') || '10' FROM DUAL";
                            _startDate = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                        }
                    }
                    // 若今日是15~19日時，固定start_date為當月15日
                    else if (today >= 15 && today <= 19)
                    {
                        using (OracleCommand cmd = _oraConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYYMM') || '15' FROM DUAL";
                            _startDate = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                        }
                    }
                    // 若今日是20~24日時，固定start_date為當月20日
                    else if (today >= 20 && today <= 24)
                    {
                        using (OracleCommand cmd = _oraConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYYMM') || '20' FROM DUAL";
                            _startDate = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                        }
                    }
                    // 若今日是25~31日時，固定start_date為當月25日
                    else if (today >= 25 && today <= 31)
                    {
                        using (OracleCommand cmd = _oraConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYYMM') || '25' FROM DUAL";
                            _startDate = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                        }
                    }
                }
                // 執行參數月份與系統月相差2個月時
                else if (monsDiff == 2)
                {
                    // 若今日是1~31日時，固定start_date為當月1日
                    if (today >= 1 && today <= 31)
                    {
                        using (OracleCommand cmd = _oraConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYYMM') || '01' FROM DUAL";
                            _startDate = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                        }
                    }
                }
                // 執行參數月份與系統月相差3個月時
                else if (monsDiff == 3)
                {
                    // 若今日是2~31日時，固定start_date為當月2日
                    if (today >= 2 && today <= 31)
                    {
                        using (OracleCommand cmd = _oraConn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYYMM') || '02' FROM DUAL";
                            _startDate = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                        }
                    }
                }
            }

            // 因star_date不能null，所以就給系統日
            if (string.IsNullOrEmpty(_startDate))
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYYMMDD') FROM DUAL";
                    _startDate = cmd.ExecuteScalar()?.ToString() ?? string.Empty;
                }
            }

            userId = Environment.GetEnvironmentVariable("MED_USER") ?? string.Empty;
            apArg = $"{_inputBranchCode} {_inputFeeYm} {_inputHospId} {truncTable}";

            // 執行中
            if (procMark == 3)
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        DELETE ICEI_EXE_CTRL
                         WHERE BRANCH_CODE = :inputBranchCode
                           AND AP_ID       = 'icei4010b01'
                           AND AP_ARG      = :apArg
                           AND START_DATE  = TO_DATE(:startDate,'YYYYMMDD')";
                    cmd.Parameters.Add(new OracleParameter("inputBranchCode", _inputBranchCode));
                    cmd.Parameters.Add(new OracleParameter("apArg", apArg));
                    cmd.Parameters.Add(new OracleParameter("startDate", _startDate));
                    cmd.ExecuteNonQuery();
                }

                // 新增流程控制
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO ICEI_EXE_CTRL (
                               BRANCH_CODE,
                               AP_ID,
                               START_DATE,
                               AP_ARG,
                               PROC_MARK,
                               MEMO,
                               EXE_S_DATE,
                               EXE_E_DATE,
                               TXT_USER_ID )
                        SELECT :inputBranchCode,
                               'icei4010b01',
                               TO_DATE(:startDate,'YYYYMMDD'),
                               :apArg,
                               :procMark,
                               :memo,
                               SYSDATE,
                               SYSDATE,
                               :userId
                          FROM DUAL";
                    cmd.Parameters.Add(new OracleParameter("inputBranchCode", _inputBranchCode));
                    cmd.Parameters.Add(new OracleParameter("startDate", _startDate));
                    cmd.Parameters.Add(new OracleParameter("apArg", apArg));
                    cmd.Parameters.Add(new OracleParameter("procMark", procMark));
                    cmd.Parameters.Add(new OracleParameter("memo", memo));
                    cmd.Parameters.Add(new OracleParameter("userId", userId));
                    cmd.ExecuteNonQuery();
                }
            }
            else
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE ICEI_EXE_CTRL
                           SET PROC_MARK  = :procMark,
                               MEMO       = :memo,
                               EXE_E_DATE = SYSDATE
                         WHERE BRANCH_CODE = :inputBranchCode
                           AND AP_ID       = 'icei4010b01'
                           AND AP_ARG      = :apArg
                           AND TO_CHAR(START_DATE,'YYYYMMDD') = :startDate";
                    cmd.Parameters.Add(new OracleParameter("procMark", procMark));
                    cmd.Parameters.Add(new OracleParameter("memo", memo));
                    cmd.Parameters.Add(new OracleParameter("inputBranchCode", _inputBranchCode));
                    cmd.Parameters.Add(new OracleParameter("apArg", apArg));
                    cmd.Parameters.Add(new OracleParameter("startDate", _startDate));
                    cmd.ExecuteNonQuery();
                }
            }

            ExecuteNonQuery("COMMIT");
        }

        // Original: ins_assay_dtl_sts
        private static void InsAssayDtlSts(string branchCode, string hospId, string feeYm, int seasonTot)
        {
            // Get SQL100 data
            SQL100 sql100 = new SQL100
            {
                branchCode = branchCode,
                hospId = hospId,
                feeYm = feeYm.Substring(0, 6) // Convert YYYYMMDD to YYYYMM
            };

            // 112.08.15 資料備份(用以判斷是否將icei_3060_pba_ord資料轉入倉儲)
            ExecuteNonQuery($@"
                DELETE ICEI_3060_ORD_STS_BAK
                 WHERE BRANCH_CODE = '{sql100.branchCode}'
                   AND FEE_YM      = TO_DATE('{sql100.feeYm}','YYYYMM')
                   AND HOSP_ID     = '{sql100.hospId}'");

            ExecuteNonQuery($@"
                INSERT INTO ICEI_3060_ORD_STS_BAK (
                            SEASON_TOT, BRANCH_CODE, HOSP_ID, HOSP_CNT_TYPE, FEE_YM, STS_TYPE, ORDER_CODE,
                            OIPD_TYPE, REPORT_TYPE, MARK_135, REAL_QTY, REAL_QTY_OTH, REAL_QTY_3DAY,
                            REAL_QTY_3DAY_OTH, REAL_QTY_NOT3DAY, REAL_QTY_NOT3DAY_OTH, UNLOAD_QTY, APPL_QTY,
                            TXT_DATE )
                     SELECT SEASON_TOT, BRANCH_CODE, HOSP_ID, HOSP_CNT_TYPE, FEE_YM, STS_TYPE, ORDER_CODE,
                            OIPD_TYPE, REPORT_TYPE, MARK_135, REAL_QTY, REAL_QTY_OTH, REAL_QTY_3DAY,
                            REAL_QTY_3DAY_OTH, REAL_QTY_NOT3DAY, REAL_QTY_NOT3DAY_OTH, UNLOAD_QTY, APPL_QTY,
                            TXT_DATE
                       FROM ICEI_3060_ORD_STS
                      WHERE BRANCH_CODE = '{sql100.branchCode}'
                        AND FEE_YM      = TO_DATE('{sql100.feeYm}','YYYYMM')
                        AND HOSP_ID     = '{sql100.hospId}'");

            ExecuteNonQuery($@"
                DELETE icei_3060_ord_sts
                 WHERE BRANCH_CODE = '{sql100.branchCode}'
                   AND FEE_YM      = TO_DATE('{sql100.feeYm}','YYYYMM')
                   AND HOSP_ID     = '{sql100.hospId}'");

            ExecuteNonQuery($@"
                DELETE icei_3060_ord_sts_fcal
                 WHERE BRANCH_CODE = '{sql100.branchCode}'
                   AND FEE_YM      = TO_DATE('{sql100.feeYm}','YYYYMM')
                   AND HOSP_ID     = '{sql100.hospId}'");

            ExecuteNonQuery($@"
                DELETE ICEI_ASSAY_DTL_STS
                 WHERE BRANCH_CODE = '{sql100.branchCode}'
                   AND FEE_YM      = TO_DATE('{sql100.feeYm}','YYYYMM')
                   AND HOSP_ID     = '{sql100.hospId}'
                   AND INDEX_TYPE <> '3'");

            ExecuteNonQuery($@"
                DELETE ICEI_ASSAY_DTL_STS_FCAL
                 WHERE BRANCH_CODE = '{sql100.branchCode}'
                   AND FEE_YM      = TO_DATE('{sql100.feeYm}','YYYYMM')
                   AND HOSP_ID     = '{sql100.hospId}'");

            ExecuteNonQuery($@"
                DELETE ICEI_IAV_DTL_STS
                 WHERE BRANCH_CODE = '{sql100.branchCode}'
                   AND FEE_YM      = TO_DATE('{sql100.feeYm}','YYYYMM')
                   AND HOSP_ID     = '{sql100.hospId}'");

            ExecuteNonQuery($@"
                DELETE ICEI_IAV_DTL_STS_FCAL
                 WHERE BRANCH_CODE = '{sql100.branchCode}'
                   AND FEE_YM      = TO_DATE('{sql100.feeYm}','YYYYMM')
                   AND HOSP_ID     = '{sql100.hospId}'");

            // 檢驗6項目自行及交付上傳數統計
            StringBuilder strSQL = new StringBuilder();
            strSQL.AppendLine("    insert into icei_3060_ord_sts (");
            strSQL.AppendLine("           season_tot,branch_code,                ");
            strSQL.AppendLine("           hosp_id,                    ");
            strSQL.AppendLine("           hosp_cnt_type,");
            strSQL.AppendLine("           fee_ym,              ");
            strSQL.AppendLine("           sts_type,");
            strSQL.AppendLine("           order_code,oipd_type,report_type,mark_135,");
            strSQL.AppendLine("           real_qty,");
            strSQL.AppendLine("           real_qty_3day,");
            strSQL.AppendLine("           real_qty_oth,");
            strSQL.AppendLine("           real_qty_3day_oth,");
            strSQL.AppendLine("           real_qty_not3day,");
            strSQL.AppendLine("           real_qty_not3day_oth,");
            strSQL.AppendLine("           unload_qty,                 ");
            strSQL.AppendLine("           appl_qty,                 ");
            strSQL.AppendLine("           txt_date, ");
            strSQL.AppendLine("           MONITOR_MARK )");
            strSQL.AppendLine("    select season_tot,branch_code,             ");
            strSQL.AppendLine("           hosp_id,                    ");
            strSQL.AppendLine("           hosp_cnt_type,");
            strSQL.AppendLine("           fee_ym,            ");
            strSQL.AppendLine("           'A' sts_type,");
            strSQL.AppendLine("           order_code,oipd_type,report_type,mark_135, ");
            strSQL.AppendLine("           mark_1_qty, mark_2_qty, mark_3_qty, mark_4_qty, mark_5_qty, mark_6_qty, mark_7_qty,");
            strSQL.AppendLine("           nvl(mark_1_qty,0)+ nvl(mark_2_qty,0)+ nvl(mark_3_qty,0)+ nvl(mark_4_qty,0)+ nvl(mark_5_qty,0)+ nvl(mark_6_qty,0)+ nvl(mark_7_qty,0),");
            strSQL.AppendLine("           sysdate,");
            strSQL.AppendLine("           'Y' MONITOR_MARK");
            strSQL.AppendLine("      from (                      ");
            strSQL.AppendLine("              select season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,");
            strSQL.AppendLine("                     oipd_type,report_type,mark_135,order_code,");
            strSQL.AppendLine("                     case when a_ASSAY_MARK='1'  then '1'   -- 1-門診自行即時");
            strSQL.AppendLine("                          when a_ASSAY_MARK='2'   then '2'   -- 2-門診自行3日");
            strSQL.AppendLine("                          when a_ASSAY_R_MARK='3'  then '3'                  -- 3-交付即時");
            strSQL.AppendLine("                          when a_ASSAY_R_MARK='4'  then '4'                  -- 4-交付3日");
            strSQL.AppendLine("                          when a_ASSAY_MARK='5'    then '5'                  -- 5-自行非即時");
            strSQL.AppendLine("                          when a_ASSAY_r_MARK='6'  then '6'                  -- 6-交付非即時");
            strSQL.AppendLine("                          else '7' end index_type ,                          -- 7-未上傳");
            strSQL.AppendLine("                     sum(nvl(audit_qty,0)) audit_qty");
            strSQL.AppendLine("                from (");
            strSQL.AppendLine("                         select season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,a_ASSAY_mARK,a_assay_r_mark,a_assay_r_hosp_id,");
            strSQL.AppendLine("                                  sum(order_qty) audit_qty,order_code,mark_135,report_type,");
            strSQL.AppendLine("                                 case when substr(hosp_data_type,1,1)='2' then '2' else '1' end oipd_type");
            strSQL.AppendLine("                           from ICEI_3060_PBA_ORD");
            strSQL.AppendLine($"                          where  hosp_id='{sql100.hospId}' and fee_ym=to_date('{sql100.feeYm}','yyyymm')  ");
            strSQL.AppendLine("                           and a_code is not null                   ");
            strSQL.AppendLine("                           group by season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,a_ASSAY_MARK,a_assay_r_mark,a_assay_r_hosp_id,order_code,mark_135,report_type,");
            strSQL.AppendLine("                                 case when substr(hosp_data_type,1,1)='2' then '2' else '1' end");
            strSQL.AppendLine("                      ) a");
            strSQL.AppendLine("                group by season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,oipd_type,report_type,mark_135,order_code,");
            strSQL.AppendLine("                         case when a_ASSAY_MARK='1'  then '1'   ");
            strSQL.AppendLine("                              when a_ASSAY_MARK='2'  then '2'   ");
            strSQL.AppendLine("                              when a_ASSAY_R_MARK='3'  then '3' ");
            strSQL.AppendLine("                              when a_ASSAY_R_MARK='4'  then '4' ");
            strSQL.AppendLine("                              when a_ASSAY_MARK='5'    then '5' ");
            strSQL.AppendLine("                              when a_ASSAY_r_MARK='6'  then '6' ");
            strSQL.AppendLine("                              else '7' end                      ");
            strSQL.AppendLine("         ) PIVOT (");
            strSQL.AppendLine("                SUM (audit_qty)");
            strSQL.AppendLine("                FOR index_type");
            strSQL.AppendLine("                IN ('1' as mark_1_qty,");
            strSQL.AppendLine("                    '2' as mark_2_qty,");
            strSQL.AppendLine("                    '3' as mark_3_qty,");
            strSQL.AppendLine("                    '4' as mark_4_qty,");
            strSQL.AppendLine("                    '5' as mark_5_qty,");
            strSQL.AppendLine("                    '6' as mark_6_qty,");
            strSQL.AppendLine("                    '7' as mark_7_qty)");
            strSQL.AppendLine("           )");

            ExecuteNonQuery(strSQL.ToString());

            // 112.5.25 結算使用table，多hosp_data_type及m_mark欄位
            strSQL.Clear();
            strSQL.AppendLine("    insert into icei_3060_ord_sts_fcal (");
            strSQL.AppendLine("           season_tot,branch_code,                ");
            strSQL.AppendLine("           hosp_id,                    ");
            strSQL.AppendLine("           hosp_cnt_type,");
            strSQL.AppendLine("           fee_ym,              ");
            strSQL.AppendLine("           sts_type,");
            strSQL.AppendLine("           order_code,oipd_type,report_type,mark_135,");
            strSQL.AppendLine("           real_qty,");
            strSQL.AppendLine("           real_qty_3day,");
            strSQL.AppendLine("           real_qty_oth,");
            strSQL.AppendLine("           real_qty_3day_oth,");
            strSQL.AppendLine("           real_qty_not3day,");
            strSQL.AppendLine("           real_qty_not3day_oth,");
            strSQL.AppendLine("           unload_qty,                 ");
            strSQL.AppendLine("           appl_qty,                 ");
            strSQL.AppendLine("           txt_date,");
            strSQL.AppendLine("           hosp_data_type,");
            strSQL.AppendLine("           m_mark, ");
            strSQL.AppendLine("           MONITOR_MARK )");
            strSQL.AppendLine("    select season_tot,branch_code,             ");
            strSQL.AppendLine("           hosp_id,                    ");
            strSQL.AppendLine("           hosp_cnt_type,");
            strSQL.AppendLine("           fee_ym,            ");
            strSQL.AppendLine("           'A' sts_type,");
            strSQL.AppendLine("           order_code,oipd_type,report_type,mark_135, ");
            strSQL.AppendLine("           mark_1_qty, mark_2_qty, mark_3_qty, mark_4_qty, mark_5_qty, mark_6_qty, mark_7_qty,");
            strSQL.AppendLine("           nvl(mark_1_qty,0)+ nvl(mark_2_qty,0)+ nvl(mark_3_qty,0)+ nvl(mark_4_qty,0)+ nvl(mark_5_qty,0)+ nvl(mark_6_qty,0)+ nvl(mark_7_qty,0),");
            strSQL.AppendLine("           sysdate,");
            strSQL.AppendLine("           hosp_data_type, m_mark, ");
            strSQL.AppendLine("           'Y' MONITOR_MARK ");
            strSQL.AppendLine("      from (                      ");
            strSQL.AppendLine("              select season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,");
            strSQL.AppendLine("                     oipd_type,report_type,mark_135,order_code,");
            strSQL.AppendLine("                     case when a_ASSAY_MARK='1'  then '1'   -- 1-門診自行即時");
            strSQL.AppendLine("                          when a_ASSAY_MARK='2'   then '2'   -- 2-門診自行3日");
            strSQL.AppendLine("                          when a_ASSAY_R_MARK='3'  then '3'                  -- 3-交付即時");
            strSQL.AppendLine("                          when a_ASSAY_R_MARK='4'  then '4'                  -- 4-交付3日");
            strSQL.AppendLine("                          when a_ASSAY_MARK='5'    then '5'                  -- 5-自行非即時");
            strSQL.AppendLine("                          when a_ASSAY_r_MARK='6'  then '6'                  -- 6-交付非即時");
            strSQL.AppendLine("                          else '7' end index_type ,                          -- 7-未上傳");
            strSQL.AppendLine("                     sum(nvl(audit_qty,0)) audit_qty,");
            strSQL.AppendLine("                     hosp_data_type, m_mark");
            strSQL.AppendLine("                from (");
            strSQL.AppendLine("                         select season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,a_ASSAY_mARK,a_assay_r_mark,a_assay_r_hosp_id,");
            strSQL.AppendLine("                                  sum(order_qty) audit_qty,order_code,mark_135,report_type,");
            strSQL.AppendLine("                                 case when substr(hosp_data_type,1,1)='2' then '2' else '1' end oipd_type,");
            strSQL.AppendLine("                                hosp_data_type, m_mark ");
            strSQL.AppendLine("                           from ICEI_3060_PBA_ORD");
            strSQL.AppendLine($"                          where  hosp_id='{sql100.hospId}' and fee_ym=to_date('{sql100.feeYm}','yyyymm')  ");
            strSQL.AppendLine("                           and a_code is not null                   ");
            strSQL.AppendLine("                           group by season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,a_ASSAY_MARK,a_assay_r_mark,a_assay_r_hosp_id,order_code,mark_135,report_type,");
            strSQL.AppendLine("                                 case when substr(hosp_data_type,1,1)='2' then '2' else '1' end, hosp_data_type, m_mark ");
            strSQL.AppendLine("                      ) a");
            strSQL.AppendLine("                group by season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,oipd_type,report_type,mark_135,order_code,");
            strSQL.AppendLine("                         case when a_ASSAY_MARK='1'  then '1'   ");
            strSQL.AppendLine("                              when a_ASSAY_MARK='2'  then '2'   ");
            strSQL.AppendLine("                              when a_ASSAY_R_MARK='3'  then '3' ");
            strSQL.AppendLine("                              when a_ASSAY_R_MARK='4'  then '4' ");
            strSQL.AppendLine("                              when a_ASSAY_MARK='5'    then '5' ");
            strSQL.AppendLine("                              when a_ASSAY_r_MARK='6'  then '6' ");
            strSQL.AppendLine("                              else '7' end,                     ");
            strSQL.AppendLine("                         hosp_data_type, m_mark ");
            strSQL.AppendLine("         ) PIVOT (");
            strSQL.AppendLine("                SUM (audit_qty)");
            strSQL.AppendLine("                FOR index_type");
            strSQL.AppendLine("                IN ('1' as mark_1_qty,");
            strSQL.AppendLine("                    '2' as mark_2_qty,");
            strSQL.AppendLine("                    '3' as mark_3_qty,");
            strSQL.AppendLine("                    '4' as mark_4_qty,");
            strSQL.AppendLine("                    '5' as mark_5_qty,");
            strSQL.AppendLine("                    '6' as mark_6_qty,");
            strSQL.AppendLine("                    '7' as mark_7_qty)");
            strSQL.AppendLine("           )");

            ExecuteNonQuery(strSQL.ToString());

            // 影像6項目自行及交付上傳數統計
            strSQL.Clear();
            strSQL.AppendLine("    insert into icei_3060_ord_sts(");
            strSQL.AppendLine("           season_tot,branch_code,                ");
            strSQL.AppendLine("           hosp_id,                    ");
            strSQL.AppendLine("           hosp_cnt_type,");
            strSQL.AppendLine("           fee_ym,              ");
            strSQL.AppendLine("           sts_type,");
            strSQL.AppendLine("           order_code,oipd_type,report_type,mark_135,");
            strSQL.AppendLine("           real_qty,");
            strSQL.AppendLine("           real_qty_3day,");
            strSQL.AppendLine("           real_qty_oth,");
            strSQL.AppendLine("           real_qty_3day_oth,");
            strSQL.AppendLine("           real_qty_not3day,");
            strSQL.AppendLine("           real_qty_not3day_oth,");
            strSQL.AppendLine("           unload_qty,                 ");
            strSQL.AppendLine("           appl_qty,                 ");
            strSQL.AppendLine("           txt_date, ");
            strSQL.AppendLine("           MONITOR_MARK )");
            strSQL.AppendLine("    select season_tot,branch_code,             ");
            strSQL.AppendLine("           hosp_id,                    ");
            strSQL.AppendLine("           hosp_cnt_type,");
            strSQL.AppendLine("           fee_ym,            ");
            strSQL.AppendLine("           'I' sts_type,");
            strSQL.AppendLine("           order_code,oipd_type,report_type,mark_135, ");
            strSQL.AppendLine("           mark_1_qty, mark_2_qty, mark_3_qty, mark_4_qty, mark_5_qty, mark_6_qty, mark_7_qty,");
            strSQL.AppendLine("           nvl(mark_1_qty,0)+ nvl(mark_2_qty,0)+ nvl(mark_3_qty,0)+ nvl(mark_4_qty,0)+ nvl(mark_5_qty,0)+ nvl(mark_6_qty,0)+ nvl(mark_7_qty,0),");
            strSQL.AppendLine("           sysdate, ");
            strSQL.AppendLine("           'Y' MONITOR_MARK");
            strSQL.AppendLine("      from (                      ");
            strSQL.AppendLine("              select season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,");
            strSQL.AppendLine("                     oipd_type,report_type,mark_135,order_code,");
            strSQL.AppendLine("                     case when i_ASSAY_MARK='1'  then '1'   -- 1-門診自行即時");
            strSQL.AppendLine("                          when i_ASSAY_MARK='2'   then '2'   -- 2-門診自行3日");
            strSQL.AppendLine("                          when i_ASSAY_R_MARK='3'  then '3'                  -- 3-交付即時");
            strSQL.AppendLine("                          when i_ASSAY_R_MARK='4'  then '4'                  -- 4-交付3日");
            strSQL.AppendLine("                          when i_ASSAY_MARK='5'    then '5'                  -- 5-自行非即時");
            strSQL.AppendLine("                          when i_ASSAY_r_MARK='6'  then '6'                  -- 6-交付非即時");
            strSQL.AppendLine("                          else '7' end index_type ,                          -- 7-未上傳");
            strSQL.AppendLine("                     sum(nvl(audit_qty,0)) audit_qty");
            strSQL.AppendLine("                from (");
            strSQL.AppendLine("                         select season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,i_ASSAY_mARK,i_ASSAY_r_mark,i_ASSAY_r_hosp_id,");
            strSQL.AppendLine("                                  sum(order_qty) audit_qty,order_code,mark_135,report_type,");
            strSQL.AppendLine("                                 case when substr(hosp_data_type,1,1)='2' then '2' else '1' end oipd_type");
            strSQL.AppendLine("                           from ICEI_3060_PBA_ORD");
            strSQL.AppendLine($"                          where  hosp_id='{sql100.hospId}' and fee_ym=to_date('{sql100.feeYm}','yyyymm')  ");
            strSQL.AppendLine("                           and i_code is not null                   ");
            strSQL.AppendLine("                           group by season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,i_ASSAY_MARK,i_ASSAY_r_mark,i_ASSAY_r_hosp_id,order_code,mark_135,report_type,");
            strSQL.AppendLine("                                 case when substr(hosp_data_type,1,1)='2' then '2' else '1' end");
            strSQL.AppendLine("                      ) a");
            strSQL.AppendLine("                group by season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,oipd_type,report_type,mark_135,order_code,");
            strSQL.AppendLine("                         case when i_ASSAY_MARK='1'    then '1'   ");
            strSQL.AppendLine("                              when i_ASSAY_MARK='2'    then '2'   ");
            strSQL.AppendLine("                              when i_ASSAY_R_MARK='3'  then '3'   ");
            strSQL.AppendLine("                              when i_ASSAY_R_MARK='4'  then '4'   ");
            strSQL.AppendLine("                              when i_ASSAY_MARK='5'    then '5'   ");
            strSQL.AppendLine("                              when i_ASSAY_r_MARK='6'  then '6'   ");
            strSQL.AppendLine("                              else '7' end                        ");
            strSQL.AppendLine("         )");
            strSQL.AppendLine("           PIVOT (");
            strSQL.AppendLine("                SUM (audit_qty)");
            strSQL.AppendLine("                FOR index_type");
            strSQL.AppendLine("                IN ('1' as mark_1_qty,");
            strSQL.AppendLine("                    '2' as mark_2_qty,");
            strSQL.AppendLine("                    '3' as mark_3_qty,");
            strSQL.AppendLine("                    '4' as mark_4_qty,");
            strSQL.AppendLine("                    '5' as mark_5_qty,");
            strSQL.AppendLine("                    '6' as mark_6_qty,");
            strSQL.AppendLine("                    '7' as mark_7_qty)");
            strSQL.AppendLine("           )");

            ExecuteNonQuery(strSQL.ToString());

            // 112.5.25 結算使用table，多hosp_data_type及m_mark欄位
            strSQL.Clear();
            strSQL.AppendLine("    insert into icei_3060_ord_sts_fcal(");
            strSQL.AppendLine("           season_tot,branch_code,                ");
            strSQL.AppendLine("           hosp_id,                    ");
            strSQL.AppendLine("           hosp_cnt_type,");
            strSQL.AppendLine("           fee_ym,              ");
            strSQL.AppendLine("           sts_type,");
            strSQL.AppendLine("           order_code,oipd_type,report_type,mark_135,");
            strSQL.AppendLine("           real_qty,");
            strSQL.AppendLine("           real_qty_3day,");
            strSQL.AppendLine("           real_qty_oth,");
            strSQL.AppendLine("           real_qty_3day_oth,");
            strSQL.AppendLine("           real_qty_not3day,");
            strSQL.AppendLine("           real_qty_not3day_oth,");
            strSQL.AppendLine("           unload_qty,                 ");
            strSQL.AppendLine("           appl_qty,                 ");
            strSQL.AppendLine("           txt_date,");
            strSQL.AppendLine("           hosp_data_type,");
            strSQL.AppendLine("           m_mark, ");
            strSQL.AppendLine("           MONITOR_MARK )");
            strSQL.AppendLine("    select season_tot,branch_code,             ");
            strSQL.AppendLine("           hosp_id,                    ");
            strSQL.AppendLine("           hosp_cnt_type,");
            strSQL.AppendLine("           fee_ym,            ");
            strSQL.AppendLine("           'I' sts_type,");
            strSQL.AppendLine("           order_code,oipd_type,report_type,mark_135, ");
            strSQL.AppendLine("           mark_1_qty, mark_2_qty, mark_3_qty, mark_4_qty, mark_5_qty, mark_6_qty, mark_7_qty,");
            strSQL.AppendLine("           nvl(mark_1_qty,0)+ nvl(mark_2_qty,0)+ nvl(mark_3_qty,0)+ nvl(mark_4_qty,0)+ nvl(mark_5_qty,0)+ nvl(mark_6_qty,0)+ nvl(mark_7_qty,0),");
            strSQL.AppendLine("           sysdate, hosp_data_type, m_mark, ");
            strSQL.AppendLine("           'Y' MONITOR_MARK ");
            strSQL.AppendLine("      from (                      ");
            strSQL.AppendLine("              select season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,");
            strSQL.AppendLine("                     oipd_type,report_type,mark_135,order_code,");
            strSQL.AppendLine("                     case when i_ASSAY_MARK='1'  then '1'   -- 1-門診自行即時");
            strSQL.AppendLine("                          when i_ASSAY_MARK='2'   then '2'   -- 2-門診自行3日");
            strSQL.AppendLine("                          when i_ASSAY_R_MARK='3'  then '3'                  -- 3-交付即時");
            strSQL.AppendLine("                          when i_ASSAY_R_MARK='4'  then '4'                  -- 4-交付3日");
            strSQL.AppendLine("                          when i_ASSAY_MARK='5'    then '5'                  -- 5-自行非即時");
            strSQL.AppendLine("                          when i_ASSAY_r_MARK='6'  then '6'                  -- 6-交付非即時");
            strSQL.AppendLine("                          else '7' end index_type ,                          -- 7-未上傳");
            strSQL.AppendLine("                     sum(nvl(audit_qty,0)) audit_qty,");
            strSQL.AppendLine("                     hosp_data_type, m_mark");
            strSQL.AppendLine("                from (");
            strSQL.AppendLine("                         select season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,i_ASSAY_mARK,i_ASSAY_r_mark,i_ASSAY_r_hosp_id,");
            strSQL.AppendLine("                                  sum(order_qty) audit_qty,order_code,mark_135,report_type,");
            strSQL.AppendLine("                                 case when substr(hosp_data_type,1,1)='2' then '2' else '1' end oipd_type,");
            strSQL.AppendLine("                                 hosp_data_type, m_mark");
            strSQL.AppendLine("                           from ICEI_3060_PBA_ORD");
            strSQL.AppendLine($"                          where  hosp_id='{sql100.hospId}' and fee_ym=to_date('{sql100.feeYm}','yyyymm')  ");
            strSQL.AppendLine("                           and i_code is not null                   ");
            strSQL.AppendLine("                           group by season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,i_ASSAY_MARK,i_ASSAY_r_mark,i_ASSAY_r_hosp_id,order_code,mark_135,report_type,");
            strSQL.AppendLine("                                 case when substr(hosp_data_type,1,1)='2' then '2' else '1' end,");
            strSQL.AppendLine("                                 hosp_data_type, m_mark");
            strSQL.AppendLine("                      ) a");
            strSQL.AppendLine("                group by season_tot,branch_code,hosp_id,hosp_cnt_type,fee_ym,oipd_type,report_type,mark_135,order_code,");
            strSQL.AppendLine("                         case when i_ASSAY_MARK='1'    then '1'   ");
            strSQL.AppendLine("                              when i_ASSAY_MARK='2'    then '2'   ");
            strSQL.AppendLine("                              when i_ASSAY_R_MARK='3'  then '3'   ");
            strSQL.AppendLine("                              when i_ASSAY_R_MARK='4'  then '4'   ");
            strSQL.AppendLine("                              when i_ASSAY_MARK='5'    then '5'   ");
            strSQL.AppendLine("                              when i_ASSAY_r_MARK='6'  then '6'   ");
            strSQL.AppendLine("                              else '7' end,                       ");
            strSQL.AppendLine("                         hosp_data_type, m_mark");
            strSQL.AppendLine("         )");
            strSQL.AppendLine("           PIVOT (");
            strSQL.AppendLine("                SUM (audit_qty)");
            strSQL.AppendLine("                FOR index_type");
            strSQL.AppendLine("                IN ('1' as mark_1_qty,");
            strSQL.AppendLine("                    '2' as mark_2_qty,");
            strSQL.AppendLine("                    '3' as mark_3_qty,");
            strSQL.AppendLine("                    '4' as mark_4_qty,");
            strSQL.AppendLine("                    '5' as mark_5_qty,");
            strSQL.AppendLine("                    '6' as mark_6_qty,");
            strSQL.AppendLine("                    '7' as mark_7_qty)");
            strSQL.AppendLine("           )");

            ExecuteNonQuery(strSQL.ToString());

            if (string.Compare(sql100.feeYm, "202301") >= 0)
            {
                // 若有一筆 FREEZE_PROC_STATUS = '1' and FREEZE_PROC_DATE is not null
                // 表示資料已凍結
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT 'X'
                          FROM ICEI_3060_PBA_CTL
                         WHERE HOSP_ID = :hospId
                           AND FEE_YM  = TO_DATE(:feeYm,'YYYYMM')
                           AND FEE_YM >= TO_DATE('202301','YYYYMM')
                           AND ( NVL(FREEZE_PROC_STATUS,'9') = '1' and FREEZE_PROC_DATE IS NOT NULL )
                           AND ROWNUM = 1";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                    
                    object result = cmd.ExecuteScalar();
                    if (result == null) // No records found
                    {
                        ExecuteNonQuery($@"
                            DELETE ICEI_ASSAY_DTL
                             WHERE HOSP_ID   = '{sql100.hospId}'
                               AND FEE_YM    = TO_DATE('{sql100.feeYm}','YYYYMM')
                               AND FEE_YM >= TO_DATE('202301','YYYYMM')
                               AND DATA_TYPE = '1'");
                        
                        ExecuteNonQuery("COMMIT");
                    }
                }
            }

            // 呼叫IPM執行程式
            _logger.Info($"呼叫IPM執行程式：icei4010b03 {sql100.branchCode} {sql100.hospId} {sql100.feeYm}");
            
            // Original: PXX_exec_batch
            MEDM_SysLib.MEDM_ExecBatch("icei4010b03", sql100.branchCode, sql100.hospId, sql100.feeYm, null);

            // Process SQL200 data
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("select season_tot,branch_code,hosp_id,hosp_cnt_type,to_char(fee_ym,'yyyymm') fee_ym,");
                strSQL.AppendLine("       sts_type,order_code,OIPD_TYPE,report_type,mark_135,appl_qty");
                strSQL.AppendLine("  from icei_3060_ord_sts");
                strSQL.AppendLine(" where branch_code=:branchCode");
                strSQL.AppendLine("   and fee_ym=to_date(:feeYm,'yyyymm')");
                strSQL.AppendLine("   and hosp_id = :hospId");
                strSQL.AppendLine("order by  branch_code,hosp_id,hosp_cnt_type,fee_ym,sts_type,order_code,OIPD_TYPE");

                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("branchCode", sql100.branchCode));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                cmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SQL200 sql200 = new SQL200
                        {
                            seasonTot = Convert.ToInt32(reader["season_tot"]),
                            branchCode = reader["branch_code"].ToString(),
                            hospId = reader["hosp_id"].ToString(),
                            hospCntType = reader["hosp_cnt_type"].ToString(),
                            feeYm = reader["fee_ym"].ToString(),
                            stsType = reader["sts_type"].ToString(),
                            orderCode = reader["order_code"].ToString(),
                            oipdType = reader["oipd_type"].ToString(),
                            reportType = reader["report_type"].ToString(),
                            mark135 = reader["mark_135"].ToString(),
                            applQty = Convert.ToInt32(reader["appl_qty"])
                        };

                        if (sql200.stsType == "A")
                        {
                            // A. 寫入ICEI_ASSAY_DTL_STS
                            ProcessAssayDtlSts(sql200);
                        }
                        else if (sql200.stsType == "I")
                        {
                            // 寫入ICEI_IAV_DTL_STS
                            ProcessIavDtlSts(sql200);
                        }
                    }
                }
            }

            // Process SQL300 data
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.Clear();
                strSQL.AppendLine("select season_tot,branch_code,hosp_id,hosp_cnt_type,to_char(fee_ym,'yyyymm') fee_ym,");
                strSQL.AppendLine("       sts_type,order_code,OIPD_TYPE,report_type,mark_135,appl_qty,hosp_data_type,m_mark");
                strSQL.AppendLine("  from icei_3060_ord_sts_fcal");
                strSQL.AppendLine(" where branch_code=:branchCode");
                strSQL.AppendLine("   and fee_ym=to_date(:feeYm,'yyyymm')");
                strSQL.AppendLine("   and hosp_id = :hospId");
                strSQL.AppendLine("order by branch_code,hosp_id,hosp_cnt_type,fee_ym,sts_type,order_code,OIPD_TYPE,hosp_data_type,m_mark");

                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("branchCode", sql100.branchCode));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                cmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SQL300 sql300 = new SQL300
                        {
                            seasonTot = Convert.ToInt32(reader["season_tot"]),
                            branchCode = reader["branch_code"].ToString(),
                            hospId = reader["hosp_id"].ToString(),
                            hospCntType = reader["hosp_cnt_type"].ToString(),
                            feeYm = reader["fee_ym"].ToString(),
                            stsType = reader["sts_type"].ToString(),
                            orderCode = reader["order_code"].ToString(),
                            oipdType = reader["oipd_type"].ToString(),
                            reportType = reader["report_type"].ToString(),
                            mark135 = reader["mark_135"].ToString(),
                            applQty = Convert.ToInt32(reader["appl_qty"]),
                            hospDataType = reader["hosp_data_type"].ToString(),
                            mMark = reader["m_mark"].ToString()
                        };

                        if (sql300.stsType == "A")
                        {
                            // A. 寫入ICEI_ASSAY_DTL_STS_FCAL
                            ProcessAssayDtlStsFcal(sql300);
                        }
                        else if (sql300.stsType == "I")
                        {
                            // 寫入ICEI_IAV_DTL_STS_FCAL
                            ProcessIavDtlStsFcal(sql300);
                        }
                    }
                }
            }

            // 112.10.20
            // 部分項目實務上目的為臨床治療行為，或記載於病歷、手術紀錄、護理紀錄或備血紀錄無報告或無實際上傳之必要性。
            // 經蒐集區域級以上醫院及台灣醫院協會之意見，擬訂排除監測之項目如附表1(檢驗(查))、2(醫療檢查影像)，
            // 將自 費用年月112年11月(含)起不予列計於上傳率計算。
            if (string.Compare(sql100.feeYm, "202311") >= 0)
            {
                UpdateMonitorMark(sql100.branchCode, sql100.hospId, sql100.feeYm);
            }
        }

        private static void ProcessAssayDtlSts(SQL200 sql200)
        {
            int hIndex1Recd = 0;
            int hIndex5Recd = 0;

            // Insert into ICEI_ASSAY_DTL_STS
            StringBuilder strSQL = new StringBuilder();
            strSQL.AppendLine("insert into ICEI_ASSAY_DTL_STS (");
            strSQL.AppendLine("        BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("        REPORT_TYPE,");
            strSQL.AppendLine("        index_type,");
            strSQL.AppendLine("        OIPD_TYPE,");
            strSQL.AppendLine("        order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("        APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("        MONITOR_MARK)");
            strSQL.AppendLine("select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("       REPORT_TYPE,");
            strSQL.AppendLine("       index_type ,");
            strSQL.AppendLine("       OIPD_TYPE,");
            strSQL.AppendLine("       order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("       case when index_type in ('1','5','6','7','C') then :applQty");
            strSQL.AppendLine("            else audit_qty end ,");
            strSQL.AppendLine("       audit_qty, MARK_135, a_ASSAY_R_hosp_id , SYSDATE TXT_DATE,");
            strSQL.AppendLine("       'Y'");
            strSQL.AppendLine("  from (");
            strSQL.AppendLine("       select a.BRANCH_CODE,a.HOSP_ID,FEE_YM,");
            strSQL.AppendLine("                REPORT_TYPE,");
            strSQL.AppendLine("                case when a_ASSAY_MARK='1' and oipd_type='1' then '1'   -- 1-門診自行即時");
            strSQL.AppendLine("                     when a_ASSAY_MARK='1' and oipd_type='2' then '5'   -- 1-住診自行即時");
            strSQL.AppendLine("                     when a_ASSAY_MARK='2' and oipd_type='1' then '6'   -- 2-門診自行3日");
            strSQL.AppendLine("                     when a_ASSAY_MARK='2' and oipd_type='2' then '7'   -- 2-住診自行3日");
            strSQL.AppendLine("                     when a_ASSAY_R_MARK='3'  then '8'                  -- 3-交付即時");
            strSQL.AppendLine("                     when a_ASSAY_R_MARK='4'  then '9'                  -- 4-交付3日");
            strSQL.AppendLine("                     when a_ASSAY_MARK='5'    then 'C'                  -- 5-自行非即時");
            strSQL.AppendLine("                     when a_ASSAY_r_MARK='6'  then 'D'                  -- 6-交付非即時");
            strSQL.AppendLine("                     else 'E' end index_type ,                          -- 7-未上傳");
            strSQL.AppendLine("                OIPD_TYPE,");
            strSQL.AppendLine("                order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("                sum(audit_qty) audit_qty ,");
            strSQL.AppendLine("                MARK_135,");
            strSQL.AppendLine("                case when a_ASSAY_MARK='1' and oipd_type='1' then a.hosp_id  -- 1-門診自行即時");
            strSQL.AppendLine("                     when a_ASSAY_MARK='1' and oipd_type='2' then a.hosp_id  -- 1-住診自行即時");
            strSQL.AppendLine("                     when a_ASSAY_MARK='2' and oipd_type='1' then a.hosp_id  -- 2-門診自行3日");
            strSQL.AppendLine("                     when a_ASSAY_MARK='2' and oipd_type='2' then a.hosp_id  -- 2-住診自行3日");
            strSQL.AppendLine("                     when a_ASSAY_R_MARK='3'  then a_ASSAY_R_hosp_id         -- 3-交付即時");
            strSQL.AppendLine("                     when a_ASSAY_R_MARK='4'  then a_ASSAY_R_hosp_id         -- 4-交付3日");
            strSQL.AppendLine("                     when a_ASSAY_MARK='5'  then a.hosp_id                   -- 5-自行非即時");
            strSQL.AppendLine("                     when a_ASSAY_r_MARK='6'  then a_ASSAY_R_hosp_id         -- 6-交付非即時");
            strSQL.AppendLine("                     else 'XXXXXXXXXX' end a_ASSAY_R_hosp_id ,               -- 7-未上傳");
            strSQL.AppendLine("                 SYSDATE TXT_DATE");
            strSQL.AppendLine("       from (");
            strSQL.AppendLine("                 select branch_code,hosp_id,fee_ym,hosp_cnt_type,a_ASSAY_mARK,a_assay_r_mark,a_assay_r_hosp_id,");
            strSQL.AppendLine("                          sum(nvl(order_qty,0)) audit_qty,order_code,season_tot,mark_135,report_type,");
            strSQL.AppendLine("                         case when substr(hosp_data_type,1,1)='2' then '2' else '1' end oipd_type");
            strSQL.AppendLine("                   from ICEI_3060_PBA_ORD");
            strSQL.AppendLine("                  where hosp_id=:hospId");
            strSQL.AppendLine("                    and fee_ym=to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("                    and order_code=:orderCode");
            strSQL.AppendLine("                    and (case when substr(hosp_data_type,1,1)='2' then '2' else '1' end) =:oipdType");
            strSQL.AppendLine("                    and a_code is not null");
            strSQL.AppendLine("                   group by branch_code,hosp_id,fee_ym,hosp_cnt_type,order_code,report_type,season_tot,mark_135,");
            strSQL.AppendLine("                      a_ASSAY_MARK,a_assay_r_mark,a_assay_r_hosp_id,");
            strSQL.AppendLine("                            case when substr(hosp_data_type,1,1)='2' then '2' else '1' end");
            strSQL.AppendLine("             ) a");
            strSQL.AppendLine("       group by a.BRANCH_CODE,a.HOSP_ID,FEE_YM,");
            strSQL.AppendLine("                 REPORT_TYPE,");
            strSQL.AppendLine("                 case when a_ASSAY_MARK='1' and oipd_type='1' then '1'  -- 1-門診自行即時");
            strSQL.AppendLine("                      when a_ASSAY_MARK='1' and oipd_type='2' then '5'  -- 1-住診自行即時");
            strSQL.AppendLine("                      when a_ASSAY_MARK='2' and oipd_type='1' then '6'  -- 2-門診自行3日");
            strSQL.AppendLine("                      when a_ASSAY_MARK='2' and oipd_type='2' then '7'  -- 2-住診自行3日");
            strSQL.AppendLine("                      when a_ASSAY_R_MARK='3'  then '8'                 -- 3-交付即時");
            strSQL.AppendLine("                      when a_ASSAY_R_MARK='4'  then '9'                 -- 4-交付3日");
            strSQL.AppendLine("                      when a_ASSAY_MARK='5'  then 'C'                   -- 5-自行非即時");
            strSQL.AppendLine("                      when a_ASSAY_r_MARK='6'  then 'D'                 -- 6-交付非即時");
            strSQL.AppendLine("                      else 'E' end  ,                                   -- 7-未上傳");
            strSQL.AppendLine("                 OIPD_TYPE,order_code,SEASON_TOT,HOSP_CNT_TYPE, MARK_135,");
            strSQL.AppendLine("                 case when a_ASSAY_MARK='1' and oipd_type='1' then a.hosp_id  -- 1-門診自行即時");
            strSQL.AppendLine("                      when a_ASSAY_MARK='1' and oipd_type='2' then a.hosp_id  -- 1-住診自行即時");
            strSQL.AppendLine("                      when a_ASSAY_MARK='2' and oipd_type='1' then a.hosp_id  -- 2-門診自行3日");
            strSQL.AppendLine("                      when a_ASSAY_MARK='2' and oipd_type='2' then a.hosp_id  -- 2-住診自行3日");
            strSQL.AppendLine("                      when a_ASSAY_R_MARK='3'  then a_ASSAY_R_hosp_id         -- 3-交付即時");
            strSQL.AppendLine("                      when a_ASSAY_R_MARK='4'  then a_ASSAY_R_hosp_id         -- 4-交付3日");
            strSQL.AppendLine("                      when a_ASSAY_MARK='5'  then a.hosp_id                   -- 5-自行非即時");
            strSQL.AppendLine("                      when a_ASSAY_r_MARK='6'  then a_ASSAY_R_hosp_id         -- 6-交付非即時");
            strSQL.AppendLine("                      else 'XXXXXXXXXX' end                                   -- 7-未上傳");
            strSQL.AppendLine("       )");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                cmd.ExecuteNonQuery();
            }

            // 112/04/24先檢查是否有自行上傳INDEX=1,5的資料,若沒有要補入,其上傳數預設為0,
            // 以利分區業務組現行程式統計
            if (sql200.oipdType == "1")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        select count(*)
                          from ICEI_ASSAY_DTL_STS
                         where hosp_id=:hospId
                           and fee_ym=to_date(:feeYm,'yyyymm')
                           and order_code=:orderCode
                           and oipd_type=:oipdType
                           and index_type='1'";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                    cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                    cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                    hIndex1Recd = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (hIndex1Recd == 0)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            insert into ICEI_ASSAY_DTL_STS (
                                   BRANCH_CODE,HOSP_ID,FEE_YM,
                                   REPORT_TYPE,
                                   index_type,
                                   OIPD_TYPE,
                                   order_code,SEASON_TOT,HOSP_CNT_TYPE,
                                   APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,
                                   MONITOR_MARK)
                            values(:branchCode,:hospId,to_date(:feeYm,'yyyymm'),
                                   :reportType,
                                   '1',
                                   :oipdType,
                                   :orderCode,:seasonTot,:hospCntType,
                                   :applQty, 0,:mark135,:hospId,sysdate,
                                   'Y')";
                        cmd.Parameters.Add(new OracleParameter("branchCode", sql200.branchCode));
                        cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                        cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                        cmd.Parameters.Add(new OracleParameter("reportType", sql200.reportType));
                        cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                        cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                        cmd.Parameters.Add(new OracleParameter("seasonTot", sql200.seasonTot));
                        cmd.Parameters.Add(new OracleParameter("hospCntType", sql200.hospCntType));
                        cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                        cmd.Parameters.Add(new OracleParameter("mark135", sql200.mark135));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            else if (sql200.oipdType == "2")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        select count(*)
                          from ICEI_ASSAY_DTL_STS
                         where hosp_id=:hospId
                           and fee_ym=to_date(:feeYm,'yyyymm')
                           and order_code=:orderCode
                           and oipd_type=:oipdType
                           and index_type='5'";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                    cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                    cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                    hIndex5Recd = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (hIndex5Recd == 0)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            insert into ICEI_ASSAY_DTL_STS (
                                   BRANCH_CODE,HOSP_ID,FEE_YM,
                                   REPORT_TYPE,
                                   index_type,
                                   OIPD_TYPE,
                                   order_code,SEASON_TOT,HOSP_CNT_TYPE,
                                   APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,
                                   MONITOR_MARK)
                            values(:branchCode,:hospId,to_date(:feeYm,'yyyymm'),
                                   :reportType,
                                   '5',
                                   :oipdType,
                                   :orderCode,:seasonTot,:hospCntType,
                                   :applQty, 0,:mark135,:hospId,sysdate,
                                   'Y')";
                        cmd.Parameters.Add(new OracleParameter("branchCode", sql200.branchCode));
                        cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                        cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                        cmd.Parameters.Add(new OracleParameter("reportType", sql200.reportType));
                        cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                        cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                        cmd.Parameters.Add(new OracleParameter("seasonTot", sql200.seasonTot));
                        cmd.Parameters.Add(new OracleParameter("hospCntType", sql200.hospCntType));
                        cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                        cmd.Parameters.Add(new OracleParameter("mark135", sql200.mark135));
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // Insert index_type='2'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_ASSAY_DTL_STS (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            MONITOR_MARK )");
            strSQL.AppendLine("    select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("           REPORT_TYPE,");
            strSQL.AppendLine("           '2' index_type,");
            strSQL.AppendLine("           OIPD_TYPE,");
            strSQL.AppendLine("           order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("           :applQty,sum(audit_qty),MARK_135,hosp_id, sysdate TXT_DATE,");
            strSQL.AppendLine("           'Y'");
            strSQL.AppendLine("      from ICEI_ASSAY_DTL_STS");
            strSQL.AppendLine("     where hosp_id    = :hospId");
            strSQL.AppendLine("       and fee_ym     = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("       and order_code = :orderCode");
            strSQL.AppendLine("       and oipd_type  = :oipdType");
            strSQL.AppendLine("       and index_type in ('1','5','6','7','C')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("           REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("           order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("           MARK_135");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                cmd.ExecuteNonQuery();
            }

            // Insert index_type='A'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_ASSAY_DTL_STS (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            'A' index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,'XXXXXXXXXX', sysdate TXT_DATE,");
            strSQL.AppendLine("            'Y'");
            strSQL.AppendLine("       from ICEI_ASSAY_DTL_STS");
            strSQL.AppendLine("      where hosp_id    = :hospId");
            strSQL.AppendLine("        and fee_ym     = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code = :orderCode");
            strSQL.AppendLine("        and oipd_type  = :oipdType");
            strSQL.AppendLine("        and  index_type in ('8','9','D')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("              REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("              order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("              MARK_135");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                cmd.ExecuteNonQuery();
            }

            // Insert index_type='B'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_ASSAY_DTL_STS (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            'B' index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,'XXXXXXXXXX', sysdate TXT_DATE,");
            strSQL.AppendLine("            'Y'");
            strSQL.AppendLine("       from ICEI_ASSAY_DTL_STS");
            strSQL.AppendLine("      where hosp_id    = :hospId");
            strSQL.AppendLine("        and fee_ym     = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code = :orderCode");
            strSQL.AppendLine("        and oipd_type = :oipdType");
            strSQL.AppendLine("        and index_type in ('1','5','6','7','C','8','9','D')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            MARK_135");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                cmd.ExecuteNonQuery();
            }
        }

        private static void ProcessIavDtlSts(SQL200 sql200)
        {
            int hIndex1Recd = 0;
            int hIndex5Recd = 0;

            // Insert into ICEI_IAV_DTL_STS
            StringBuilder strSQL = new StringBuilder();
            strSQL.AppendLine("insert into ICEI_IAV_DTL_STS (");
            strSQL.AppendLine("        BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("        REPORT_TYPE,");
            strSQL.AppendLine("        index_type,");
            strSQL.AppendLine("        OIPD_TYPE,");
            strSQL.AppendLine("        order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("        APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("        MONITOR_MARK )");
            strSQL.AppendLine("select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("       REPORT_TYPE,");
            strSQL.AppendLine("       index_type ,");
            strSQL.AppendLine("       OIPD_TYPE,");
            strSQL.AppendLine("       order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("       case when index_type in ('1','5','6','7','C') then :applQty");
            strSQL.AppendLine("            else audit_qty end ,");
            strSQL.AppendLine("       audit_qty,");
            strSQL.AppendLine("       MARK_135,");
            strSQL.AppendLine("       I_ASSAY_R_hosp_id , SYSDATE TXT_DATE,");
            strSQL.AppendLine("       'Y'");
            strSQL.AppendLine("  from (");
            strSQL.AppendLine("      select a.BRANCH_CODE,a.HOSP_ID,FEE_YM,");
            strSQL.AppendLine("               REPORT_TYPE,");
            strSQL.AppendLine("               case when I_ASSAY_MARK='1' and oipd_type='1' then '1'   -- 1-門診自行即時");
            strSQL.AppendLine("                    when I_ASSAY_MARK='1' and oipd_type='2' then '5'   -- 1-住診自行即時");
            strSQL.AppendLine("                    when I_ASSAY_MARK='2' and oipd_type='1' then '6'   -- 2-門診自行3日");
            strSQL.AppendLine("                    when I_ASSAY_MARK='2' and oipd_type='2' then '7'   -- 2-住診自行3日");
            strSQL.AppendLine("                    when I_ASSAY_R_MARK='3'  then '8'                  -- 3-交付即時");
            strSQL.AppendLine("                    when I_ASSAY_R_MARK='4'  then '9'                  -- 4-交付3日");
            strSQL.AppendLine("                    when I_ASSAY_MARK='5'    then 'C'                  -- 5-自行非即時");
            strSQL.AppendLine("                    when I_ASSAY_r_MARK='6'  then 'D'                  -- 6-交付非即時");
            strSQL.AppendLine("                    else 'E' end index_type ,                          -- 7-未上傳");
            strSQL.AppendLine("               OIPD_TYPE,");
            strSQL.AppendLine("               order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("               sum(audit_qty) audit_qty ,");
            strSQL.AppendLine("               MARK_135,");
            strSQL.AppendLine("               case when I_ASSAY_MARK='1' and oipd_type='1' then a.hosp_id  -- 1-門診自行即時");
            strSQL.AppendLine("                    when I_ASSAY_MARK='1' and oipd_type='2' then a.hosp_id  -- 1-住診自行即時");
            strSQL.AppendLine("                    when I_ASSAY_MARK='2' and oipd_type='1' then a.hosp_id  -- 2-門診自行3日");
            strSQL.AppendLine("                    when I_ASSAY_MARK='2' and oipd_type='2' then a.hosp_id  -- 2-住診自行3日");
            strSQL.AppendLine("                    when I_ASSAY_R_MARK='3'  then I_ASSAY_R_hosp_id         -- 3-交付即時");
            strSQL.AppendLine("                    when I_ASSAY_R_MARK='4'  then I_ASSAY_R_hosp_id         -- 4-交付3日");
            strSQL.AppendLine("                    when I_ASSAY_MARK='5'  then a.hosp_id                   -- 5-自行非即時");
            strSQL.AppendLine("                    when I_ASSAY_r_MARK='6'  then I_ASSAY_R_hosp_id         -- 6-交付非即時");
            strSQL.AppendLine("                    else 'XXXXXXXXXX' end I_ASSAY_R_hosp_id ,               -- 7-未上傳");
            strSQL.AppendLine("                SYSDATE TXT_DATE");
            strSQL.AppendLine("      from (");
            strSQL.AppendLine("                select branch_code,hosp_id,fee_ym,hosp_cnt_type,I_ASSAY_mARK,I_ASSAY_r_mark,I_ASSAY_r_hosp_id,");
            strSQL.AppendLine("                         sum(nvl(order_qty,0)) audit_qty,order_code,season_tot,mark_135,report_type,");
            strSQL.AppendLine("                        case when substr(hosp_data_type,1,1)='2' then '2' else '1' end oipd_type");
            strSQL.AppendLine("                  from ICEI_3060_PBA_ORD");
            strSQL.AppendLine("                 where hosp_id=:hospId");
            strSQL.AppendLine("                   and fee_ym=to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("                   and order_code=:orderCode");
            strSQL.AppendLine("                   and (case when substr(hosp_data_type,1,1)='2' then '2' else '1' end) =:oipdType");
            strSQL.AppendLine("                   and i_code is not null");
            strSQL.AppendLine("                  group by branch_code,hosp_id,fee_ym,hosp_cnt_type,order_code,report_type,season_tot,mark_135,");
            strSQL.AppendLine("                      I_ASSAY_MARK,I_ASSAY_r_mark,I_ASSAY_r_hosp_id,");
            strSQL.AppendLine("                           case when substr(hosp_data_type,1,1)='2' then '2' else '1' end");
            strSQL.AppendLine("            ) a");
            strSQL.AppendLine("      group by a.BRANCH_CODE,a.HOSP_ID,FEE_YM,");
            strSQL.AppendLine("                REPORT_TYPE,");
            strSQL.AppendLine("                case when I_ASSAY_MARK='1' and oipd_type='1' then '1'  -- 1-門診自行即時");
            strSQL.AppendLine("                     when I_ASSAY_MARK='1' and oipd_type='2' then '5'  -- 1-住診自行即時");
            strSQL.AppendLine("                     when I_ASSAY_MARK='2' and oipd_type='1' then '6'  -- 2-門診自行3日");
            strSQL.AppendLine("                     when I_ASSAY_MARK='2' and oipd_type='2' then '7'  -- 2-住診自行3日");
            strSQL.AppendLine("                     when I_ASSAY_R_MARK='3'  then '8'                 -- 3-交付即時");
            strSQL.AppendLine("                     when I_ASSAY_R_MARK='4'  then '9'                 -- 4-交付3日");
            strSQL.AppendLine("                     when I_ASSAY_MARK='5'  then 'C'                   -- 5-自行非即時");
            strSQL.AppendLine("                     when I_ASSAY_r_MARK='6'  then 'D'                 -- 6-交付非即時");
            strSQL.AppendLine("                     else 'E' end  ,                                   -- 7-未上傳");
            strSQL.AppendLine("                OIPD_TYPE,order_code,SEASON_TOT,HOSP_CNT_TYPE, MARK_135,");
            strSQL.AppendLine("                case when I_ASSAY_MARK='1' and oipd_type='1' then a.hosp_id  -- 1-門診自行即時");
            strSQL.AppendLine("                     when I_ASSAY_MARK='1' and oipd_type='2' then a.hosp_id  -- 1-住診自行即時");
            strSQL.AppendLine("                     when I_ASSAY_MARK='2' and oipd_type='1' then a.hosp_id  -- 2-門診自行3日");
            strSQL.AppendLine("                     when I_ASSAY_MARK='2' and oipd_type='2' then a.hosp_id  -- 2-住診自行3日");
            strSQL.AppendLine("                     when I_ASSAY_R_MARK='3'  then I_ASSAY_R_hosp_id         -- 3-交付即時");
            strSQL.AppendLine("                     when I_ASSAY_R_MARK='4'  then I_ASSAY_R_hosp_id         -- 4-交付3日");
            strSQL.AppendLine("                     when I_ASSAY_MARK='5'  then a.hosp_id                   -- 5-自行非即時");
            strSQL.AppendLine("                     when I_ASSAY_r_MARK='6'  then I_ASSAY_R_hosp_id         -- 6-交付非即時");
            strSQL.AppendLine("                     else 'XXXXXXXXXX' end                                   -- 7-未上傳");
            strSQL.AppendLine("       )");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                cmd.ExecuteNonQuery();
            }

            // 112/04/24先檢查是否有自行上傳INDEX=1,5的資料,若沒有要補入,其上傳數預設為0,
            // 以利分區業務組現行程式統計
            if (sql200.oipdType == "1")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        select count(*)
                          from ICEI_IAV_DTL_STS
                         where hosp_id=:hospId
                           and fee_ym=to_date(:feeYm,'yyyymm')
                           and order_code=:orderCode
                           and oipd_type=:oipdType
                           and index_type='1'";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                    cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                    cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                    hIndex1Recd = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (hIndex1Recd == 0)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            insert into ICEI_IAV_DTL_STS (
                                   BRANCH_CODE,HOSP_ID,FEE_YM,
                                   REPORT_TYPE,
                                   index_type,
                                   OIPD_TYPE,
                                   order_code,SEASON_TOT,HOSP_CNT_TYPE,
                                   APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,
                                   MONITOR_MARK )
                            values(:branchCode,:hospId,to_date(:feeYm,'yyyymm'),
                                   :reportType,
                                   '1',
                                   :oipdType,
                                   :orderCode,:seasonTot,:hospCntType,
                                   :applQty, 0,:mark135,:hospId,sysdate,
                                   'Y')";
                        cmd.Parameters.Add(new OracleParameter("branchCode", sql200.branchCode));
                        cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                        cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                        cmd.Parameters.Add(new OracleParameter("reportType", sql200.reportType));
                        cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                        cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                        cmd.Parameters.Add(new OracleParameter("seasonTot", sql200.seasonTot));
                        cmd.Parameters.Add(new OracleParameter("hospCntType", sql200.hospCntType));
                        cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                        cmd.Parameters.Add(new OracleParameter("mark135", sql200.mark135));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            else if (sql200.oipdType == "2")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        select count(*)
                          from ICEI_IAV_DTL_STS
                         where hosp_id=:hospId
                           and fee_ym=to_date(:feeYm,'yyyymm')
                           and order_code=:orderCode
                           and oipd_type=:oipdType
                           and index_type='5'";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                    cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                    cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                    hIndex5Recd = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (hIndex5Recd == 0)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            insert into ICEI_IAV_DTL_STS (
                                   BRANCH_CODE,HOSP_ID,FEE_YM,
                                   REPORT_TYPE,
                                   index_type,
                                   OIPD_TYPE,
                                   order_code,SEASON_TOT,HOSP_CNT_TYPE,
                                   APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,
                                   MONITOR_MARK )
                            values(:branchCode,:hospId,to_date(:feeYm,'yyyymm'),
                                   :reportType,
                                   '5',
                                   :oipdType,
                                   :orderCode,:seasonTot,:hospCntType,
                                   :applQty, 0,:mark135,:hospId,sysdate,
                                   'Y')";
                        cmd.Parameters.Add(new OracleParameter("branchCode", sql200.branchCode));
                        cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                        cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                        cmd.Parameters.Add(new OracleParameter("reportType", sql200.reportType));
                        cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                        cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                        cmd.Parameters.Add(new OracleParameter("seasonTot", sql200.seasonTot));
                        cmd.Parameters.Add(new OracleParameter("hospCntType", sql200.hospCntType));
                        cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                        cmd.Parameters.Add(new OracleParameter("mark135", sql200.mark135));
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // Insert index_type='2'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_IAV_DTL_STS (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            MONITOR_MARK )");
            strSQL.AppendLine("    select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("           REPORT_TYPE,");
            strSQL.AppendLine("           '2' index_type,");
            strSQL.AppendLine("           OIPD_TYPE,");
            strSQL.AppendLine("           order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("           :applQty,sum(audit_qty),MARK_135,hosp_id, sysdate TXT_DATE,");
            strSQL.AppendLine("           'Y'");
            strSQL.AppendLine("      from ICEI_IAV_DTL_STS");
            strSQL.AppendLine("     where hosp_id    = :hospId");
            strSQL.AppendLine("       and fee_ym     = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("       and order_code = :orderCode");
            strSQL.AppendLine("       and oipd_type  = :oipdType");
            strSQL.AppendLine("       and index_type in ('1','5','6','7','C')");
            strSQL.AppendLine("    group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("             REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("             order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("             MARK_135");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                cmd.ExecuteNonQuery();
            }

            // Insert index_type='A'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_IAV_DTL_STS (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            'A' index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,'XXXXXXXXXX', sysdate TXT_DATE,");
            strSQL.AppendLine("            'Y'");
            strSQL.AppendLine("       from ICEI_IAV_DTL_STS");
            strSQL.AppendLine("      where hosp_id    = :hospId");
            strSQL.AppendLine("        and fee_ym     = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code = :orderCode");
            strSQL.AppendLine("        and oipd_type  = :oipdType");
            strSQL.AppendLine("        and  index_type in ('8','9','D')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("              REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("              order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("              MARK_135");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                cmd.ExecuteNonQuery();
            }

            // Insert index_type='B'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_IAV_DTL_STS (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,'B' index_type,OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,'XXXXXXXXXX', sysdate TXT_DATE,");
            strSQL.AppendLine("            'Y'");
            strSQL.AppendLine("       from ICEI_IAV_DTL_STS");
            strSQL.AppendLine("      where hosp_id    = :hospId");
            strSQL.AppendLine("        and fee_ym     = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code = :orderCode");
            strSQL.AppendLine("        and oipd_type  = :oipdType");
            strSQL.AppendLine("        and  index_type in ('1','5','6','7','C','8','9','D')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("              REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("              order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("              MARK_135");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql200.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql200.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql200.oipdType));
                cmd.ExecuteNonQuery();
            }
        }

        private static void ProcessAssayDtlStsFcal(SQL300 sql300)
        {
            int hIndex1Recd = 0;
            int hIndex5Recd = 0;

            // Insert into ICEI_ASSAY_DTL_STS_FCAL
            StringBuilder strSQL = new StringBuilder();
            strSQL.AppendLine("insert into ICEI_ASSAY_DTL_STS_FCAL (");
            strSQL.AppendLine("        BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("        REPORT_TYPE,");
            strSQL.AppendLine("        index_type,");
            strSQL.AppendLine("        OIPD_TYPE,");
            strSQL.AppendLine("        order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("        APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("        HOSP_DATA_TYPE, M_MARK,");
            strSQL.AppendLine("        MONITOR_MARK)");
            strSQL.AppendLine("select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("       REPORT_TYPE,");
            strSQL.AppendLine("       index_type ,");
            strSQL.AppendLine("       OIPD_TYPE,");
            strSQL.AppendLine("       order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("       case when index_type in ('1','5','6','7','C') then :applQty");
            strSQL.AppendLine("            else audit_qty end ,");
            strSQL.AppendLine("       audit_qty, MARK_135, a_ASSAY_R_hosp_id , SYSDATE TXT_DATE,");
            strSQL.AppendLine("       HOSP_DATA_TYPE, M_MARK,");
            strSQL.AppendLine("       'Y'");
            strSQL.AppendLine("  from (");
            strSQL.AppendLine("       select a.BRANCH_CODE,a.HOSP_ID,FEE_YM,");
            strSQL.AppendLine("                REPORT_TYPE,");
            strSQL.AppendLine("                case when a_ASSAY_MARK='1' and oipd_type='1' then '1'   -- 1-門診自行即時");
            strSQL.AppendLine("                     when a_ASSAY_MARK='1' and oipd_type='2' then '5'   -- 1-住診自行即時");
            strSQL.AppendLine("                     when a_ASSAY_MARK='2' and oipd_type='1' then '6'   -- 2-門診自行3日");
            strSQL.AppendLine("                     when a_ASSAY_MARK='2' and oipd_type='2' then '7'   -- 2-住診自行3日");
            strSQL.AppendLine("                     when a_ASSAY_R_MARK='3'  then '8'                  -- 3-交付即時");
            strSQL.AppendLine("                     when a_ASSAY_R_MARK='4'  then '9'                  -- 4-交付3日");
            strSQL.AppendLine("                     when a_ASSAY_MARK='5'    then 'C'                  -- 5-自行非即時");
            strSQL.AppendLine("                     when a_ASSAY_r_MARK='6'  then 'D'                  -- 6-交付非即時");
            strSQL.AppendLine("                     else 'E' end index_type ,                          -- 7-未上傳");
            strSQL.AppendLine("                OIPD_TYPE,");
            strSQL.AppendLine("                order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("                sum(audit_qty) audit_qty ,");
            strSQL.AppendLine("                MARK_135,");
            strSQL.AppendLine("                case when a_ASSAY_MARK='1' and oipd_type='1' then a.hosp_id  -- 1-門診自行即時");
            strSQL.AppendLine("                     when a_ASSAY_MARK='1' and oipd_type='2' then a.hosp_id  -- 1-住診自行即時");
            strSQL.AppendLine("                     when a_ASSAY_MARK='2' and oipd_type='1' then a.hosp_id  -- 2-門診自行3日");
            strSQL.AppendLine("                     when a_ASSAY_MARK='2' and oipd_type='2' then a.hosp_id  -- 2-住診自行3日");
            strSQL.AppendLine("                     when a_ASSAY_R_MARK='3'  then a_ASSAY_R_hosp_id         -- 3-交付即時");
            strSQL.AppendLine("                     when a_ASSAY_R_MARK='4'  then a_ASSAY_R_hosp_id         -- 4-交付3日");
            strSQL.AppendLine("                     when a_ASSAY_MARK='5'  then a.hosp_id                   -- 5-自行非即時");
            strSQL.AppendLine("                     when a_ASSAY_r_MARK='6'  then a_ASSAY_R_hosp_id         -- 6-交付非即時");
            strSQL.AppendLine("                     else 'XXXXXXXXXX' end a_ASSAY_R_hosp_id ,               -- 7-未上傳");
            strSQL.AppendLine("                 SYSDATE TXT_DATE,");
            strSQL.AppendLine("                 HOSP_DATA_TYPE, M_MARK");
            strSQL.AppendLine("       from (");
            strSQL.AppendLine("                 select branch_code,hosp_id,fee_ym,hosp_cnt_type,a_ASSAY_mARK,a_assay_r_mark,a_assay_r_hosp_id,");
            strSQL.AppendLine("                        sum(nvl(order_qty,0)) audit_qty,order_code,season_tot,mark_135,report_type,");
            strSQL.AppendLine("                        case when substr(hosp_data_type,1,1)='2' then '2' else '1' end oipd_type,");
            strSQL.AppendLine("                        HOSP_DATA_TYPE, M_MARK");
            strSQL.AppendLine("                   from ICEI_3060_PBA_ORD");
            strSQL.AppendLine("                  where hosp_id=:hospId");
            strSQL.AppendLine("                    and fee_ym=to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("                    and order_code=:orderCode");
            strSQL.AppendLine("                    and (case when substr(hosp_data_type,1,1)='2' then '2' else '1' end) =:oipdType");
            strSQL.AppendLine("                    and a_code is not null");
            strSQL.AppendLine("                    AND HOSP_DATA_TYPE = :hospDataType");
            strSQL.AppendLine("                    AND M_MARK         = :mMark");
            strSQL.AppendLine("                   group by branch_code,hosp_id,fee_ym,hosp_cnt_type,order_code,report_type,season_tot,mark_135,");
            strSQL.AppendLine("                            a_ASSAY_MARK,a_assay_r_mark,a_assay_r_hosp_id,");
            strSQL.AppendLine("                            case when substr(hosp_data_type,1,1)='2' then '2' else '1' end,");
            strSQL.AppendLine("                            HOSP_DATA_TYPE, M_MARK");
            strSQL.AppendLine("             ) a");
            strSQL.AppendLine("       group by a.BRANCH_CODE,a.HOSP_ID,FEE_YM,");
            strSQL.AppendLine("                 REPORT_TYPE,");
            strSQL.AppendLine("                 case when a_ASSAY_MARK='1' and oipd_type='1' then '1'  -- 1-門診自行即時");
            strSQL.AppendLine("                      when a_ASSAY_MARK='1' and oipd_type='2' then '5'  -- 1-住診自行即時");
            strSQL.AppendLine("                      when a_ASSAY_MARK='2' and oipd_type='1' then '6'  -- 2-門診自行3日");
            strSQL.AppendLine("                      when a_ASSAY_MARK='2' and oipd_type='2' then '7'  -- 2-住診自行3日");
            strSQL.AppendLine("                      when a_ASSAY_R_MARK='3'  then '8'                 -- 3-交付即時");
            strSQL.AppendLine("                      when a_ASSAY_R_MARK='4'  then '9'                 -- 4-交付3日");
            strSQL.AppendLine("                      when a_ASSAY_MARK='5'  then 'C'                   -- 5-自行非即時");
            strSQL.AppendLine("                      when a_ASSAY_r_MARK='6'  then 'D'                 -- 6-交付非即時");
            strSQL.AppendLine("                      else 'E' end  ,                                   -- 7-未上傳");
            strSQL.AppendLine("                 OIPD_TYPE,order_code,SEASON_TOT,HOSP_CNT_TYPE, MARK_135,");
            strSQL.AppendLine("                 case when a_ASSAY_MARK='1' and oipd_type='1' then a.hosp_id  -- 1-門診自行即時");
            strSQL.AppendLine("                      when a_ASSAY_MARK='1' and oipd_type='2' then a.hosp_id  -- 1-住診自行即時");
            strSQL.AppendLine("                      when a_ASSAY_MARK='2' and oipd_type='1' then a.hosp_id  -- 2-門診自行3日");
            strSQL.AppendLine("                      when a_ASSAY_MARK='2' and oipd_type='2' then a.hosp_id  -- 2-住診自行3日");
            strSQL.AppendLine("                      when a_ASSAY_R_MARK='3'  then a_ASSAY_R_hosp_id         -- 3-交付即時");
            strSQL.AppendLine("                      when a_ASSAY_R_MARK='4'  then a_ASSAY_R_hosp_id         -- 4-交付3日");
            strSQL.AppendLine("                      when a_ASSAY_MARK='5'  then a.hosp_id                   -- 5-自行非即時");
            strSQL.AppendLine("                      when a_ASSAY_r_MARK='6'  then a_ASSAY_R_hosp_id         -- 6-交付非即時");
            strSQL.AppendLine("                      else 'XXXXXXXXXX' end,                                  -- 7-未上傳");
            strSQL.AppendLine("                HOSP_DATA_TYPE, M_MARK");
            strSQL.AppendLine("       )");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                cmd.ExecuteNonQuery();
            }

            // 112/04/24先檢查是否有自行上傳INDEX=1,5的資料,若沒有要補入,其上傳數預設為0,
            // 以利分區業務組現行程式統計
            if (sql300.oipdType == "1")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        select count(*)
                          from ICEI_ASSAY_DTL_STS_FCAL
                         where hosp_id        = :hospId
                           and fee_ym         = to_date(:feeYm,'yyyymm')
                           and order_code     = :orderCode
                           and oipd_type      = :oipdType
                           and hosp_data_type = :hospDataType
                           and m_mark         = :mMark
                           and index_type     = '1'";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                    cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                    cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                    cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                    cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                    hIndex1Recd = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (hIndex1Recd == 0)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            insert into ICEI_ASSAY_DTL_STS_FCAL (
                                    BRANCH_CODE,HOSP_ID,FEE_YM,
                                    REPORT_TYPE,
                                    index_type,
                                    OIPD_TYPE,
                                    order_code,SEASON_TOT,HOSP_CNT_TYPE,
                                    APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,
                                    HOSP_DATA_TYPE, M_MARK,
                                    MONITOR_MARK)
                            values(:branchCode,:hospId,to_date(:feeYm,'yyyymm'),
                                    :reportType,
                                    '1',
                                    :oipdType,
                                    :orderCode,:seasonTot,:hospCntType,
                                    :applQty, 0,:mark135,:hospId,sysdate,
                                    :hospDataType, :mMark,
                                    'Y')";
                        cmd.Parameters.Add(new OracleParameter("branchCode", sql300.branchCode));
                        cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                        cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                        cmd.Parameters.Add(new OracleParameter("reportType", sql300.reportType));
                        cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                        cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                        cmd.Parameters.Add(new OracleParameter("seasonTot", sql300.seasonTot));
                        cmd.Parameters.Add(new OracleParameter("hospCntType", sql300.hospCntType));
                        cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                        cmd.Parameters.Add(new OracleParameter("mark135", sql300.mark135));
                        cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                        cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            else if (sql300.oipdType == "2")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        select count(*)
                          from ICEI_ASSAY_DTL_STS_FCAL
                         where hosp_id        = :hospId
                           and fee_ym         = to_date(:feeYm,'yyyymm')
                           and order_code     = :orderCode
                           and oipd_type      = :oipdType
                           and hosp_data_type = :hospDataType
                           and m_mark         = :mMark
                           and index_type  = '5'";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                    cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                    cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                    cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                    cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                    hIndex5Recd = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (hIndex5Recd == 0)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            insert into ICEI_ASSAY_DTL_STS_FCAL (
                                    BRANCH_CODE,HOSP_ID,FEE_YM,
                                    REPORT_TYPE,
                                    index_type,
                                    OIPD_TYPE,
                                    order_code,SEASON_TOT,HOSP_CNT_TYPE,
                                    APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,
                                    HOSP_DATA_TYPE, M_MARK, MONITOR_MARK)
                            values(:branchCode,:hospId,to_date(:feeYm,'yyyymm'),
                                    :reportType,
                                    '5',
                                    :oipdType,
                                    :orderCode,:seasonTot,:hospCntType,
                                    :applQty, 0,:mark135,:hospId,sysdate,
                                    :hospDataType, :mMark, 'Y')";
                        cmd.Parameters.Add(new OracleParameter("branchCode", sql300.branchCode));
                        cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                        cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                        cmd.Parameters.Add(new OracleParameter("reportType", sql300.reportType));
                        cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                        cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                        cmd.Parameters.Add(new OracleParameter("seasonTot", sql300.seasonTot));
                        cmd.Parameters.Add(new OracleParameter("hospCntType", sql300.hospCntType));
                        cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                        cmd.Parameters.Add(new OracleParameter("mark135", sql300.mark135));
                        cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                        cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // Insert index_type='2'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_ASSAY_DTL_STS_FCAL (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            '2' index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,hosp_id, sysdate TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, 'Y'");
            strSQL.AppendLine("       from ICEI_ASSAY_DTL_STS_FCAL");
            strSQL.AppendLine("      where hosp_id        = :hospId");
            strSQL.AppendLine("        and fee_ym         = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code     = :orderCode");
            strSQL.AppendLine("        and oipd_type      = :oipdType");
            strSQL.AppendLine("        and hosp_data_type = :hospDataType");
            strSQL.AppendLine("        and m_mark         = :mMark");
            strSQL.AppendLine("        and index_type in ('1','5','6','7','C')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("              REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("              order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("              MARK_135,");
            strSQL.AppendLine("              HOSP_DATA_TYPE, M_MARK");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                cmd.ExecuteNonQuery();
            }

            // Insert index_type='A'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_ASSAY_DTL_STS_FCAL (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            'A' index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,'XXXXXXXXXX', sysdate TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, 'Y'");
            strSQL.AppendLine("       from ICEI_ASSAY_DTL_STS_FCAL");
            strSQL.AppendLine("      where hosp_id        = :hospId");
            strSQL.AppendLine("        and fee_ym         = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code     = :orderCode");
            strSQL.AppendLine("        and oipd_type      = :oipdType");
            strSQL.AppendLine("        and hosp_data_type = :hospDataType");
            strSQL.AppendLine("        and m_mark         = :mMark");
            strSQL.AppendLine("        and  index_type in ('8','9','D')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("              REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("              order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("              MARK_135,");
            strSQL.AppendLine("              HOSP_DATA_TYPE, M_MARK");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                cmd.ExecuteNonQuery();
            }

            // Insert index_type='B'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_ASSAY_DTL_STS_FCAL (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            'B' index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,'XXXXXXXXXX', sysdate TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, 'Y'");
            strSQL.AppendLine("       from ICEI_ASSAY_DTL_STS_FCAL");
            strSQL.AppendLine("      where hosp_id        = :hospId");
            strSQL.AppendLine("        and fee_ym         = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code     = :orderCode");
            strSQL.AppendLine("        and oipd_type      = :oipdType");
            strSQL.AppendLine("        and hosp_data_type = :hospDataType");
            strSQL.AppendLine("        and m_mark         = :mMark");
            strSQL.AppendLine("        and index_type in ('1','5','6','7','C','8','9','D')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            MARK_135,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                cmd.ExecuteNonQuery();
            }
        }

        private static void ProcessIavDtlStsFcal(SQL300 sql300)
        {
            int hIndex1Recd = 0;
            int hIndex5Recd = 0;

            // Insert into ICEI_IAV_DTL_STS_FCAL
            StringBuilder strSQL = new StringBuilder();
            strSQL.AppendLine("insert into ICEI_IAV_DTL_STS_FCAL (");
            strSQL.AppendLine("        BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("        REPORT_TYPE,");
            strSQL.AppendLine("        index_type,");
            strSQL.AppendLine("        OIPD_TYPE,");
            strSQL.AppendLine("        order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("        APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("        HOSP_DATA_TYPE, M_MARK,");
            strSQL.AppendLine("        MONITOR_MARK)");
            strSQL.AppendLine("select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("       REPORT_TYPE,");
            strSQL.AppendLine("       index_type ,");
            strSQL.AppendLine("       OIPD_TYPE,");
            strSQL.AppendLine("       order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("       case when index_type in ('1','5','6','7','C') then :applQty");
            strSQL.AppendLine("            else audit_qty end ,");
            strSQL.AppendLine("       audit_qty,");
            strSQL.AppendLine("       MARK_135,");
            strSQL.AppendLine("       I_ASSAY_R_hosp_id , SYSDATE TXT_DATE,");
            strSQL.AppendLine("       HOSP_DATA_TYPE, M_MARK,");
            strSQL.AppendLine("       'Y'");
            strSQL.AppendLine("  from (");
            strSQL.AppendLine("      select a.BRANCH_CODE,a.HOSP_ID,FEE_YM,");
            strSQL.AppendLine("               REPORT_TYPE,");
            strSQL.AppendLine("               case when I_ASSAY_MARK='1' and oipd_type='1' then '1'   -- 1-門診自行即時");
            strSQL.AppendLine("                    when I_ASSAY_MARK='1' and oipd_type='2' then '5'   -- 1-住診自行即時");
            strSQL.AppendLine("                    when I_ASSAY_MARK='2' and oipd_type='1' then '6'   -- 2-門診自行3日");
            strSQL.AppendLine("                    when I_ASSAY_MARK='2' and oipd_type='2' then '7'   -- 2-住診自行3日");
            strSQL.AppendLine("                    when I_ASSAY_R_MARK='3'  then '8'                  -- 3-交付即時");
            strSQL.AppendLine("                    when I_ASSAY_R_MARK='4'  then '9'                  -- 4-交付3日");
            strSQL.AppendLine("                    when I_ASSAY_MARK='5'    then 'C'                  -- 5-自行非即時");
            strSQL.AppendLine("                    when I_ASSAY_r_MARK='6'  then 'D'                  -- 6-交付非即時");
            strSQL.AppendLine("                    else 'E' end index_type ,                          -- 7-未上傳");
            strSQL.AppendLine("               OIPD_TYPE,");
            strSQL.AppendLine("               order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("               sum(audit_qty) audit_qty ,");
            strSQL.AppendLine("               MARK_135,");
            strSQL.AppendLine("               case when I_ASSAY_MARK='1' and oipd_type='1' then a.hosp_id  -- 1-門診自行即時");
            strSQL.AppendLine("                    when I_ASSAY_MARK='1' and oipd_type='2' then a.hosp_id  -- 1-住診自行即時");
            strSQL.AppendLine("                    when I_ASSAY_MARK='2' and oipd_type='1' then a.hosp_id  -- 2-門診自行3日");
            strSQL.AppendLine("                    when I_ASSAY_MARK='2' and oipd_type='2' then a.hosp_id  -- 2-住診自行3日");
            strSQL.AppendLine("                    when I_ASSAY_R_MARK='3'  then I_ASSAY_R_hosp_id         -- 3-交付即時");
            strSQL.AppendLine("                    when I_ASSAY_R_MARK='4'  then I_ASSAY_R_hosp_id         -- 4-交付3日");
            strSQL.AppendLine("                    when I_ASSAY_MARK='5'  then a.hosp_id                   -- 5-自行非即時");
            strSQL.AppendLine("                    when I_ASSAY_r_MARK='6'  then I_ASSAY_R_hosp_id         -- 6-交付非即時");
            strSQL.AppendLine("                    else 'XXXXXXXXXX' end I_ASSAY_R_hosp_id ,               -- 7-未上傳");
            strSQL.AppendLine("                SYSDATE TXT_DATE,");
            strSQL.AppendLine("                hosp_data_type, m_mark");
            strSQL.AppendLine("      from (");
            strSQL.AppendLine("                select branch_code,hosp_id,fee_ym,hosp_cnt_type,I_ASSAY_mARK,I_ASSAY_r_mark,I_ASSAY_r_hosp_id,");
            strSQL.AppendLine("                       sum(nvl(order_qty,0)) audit_qty,order_code,season_tot,mark_135,report_type,");
            strSQL.AppendLine("                       case when substr(hosp_data_type,1,1)='2' then '2' else '1' end oipd_type,");
            strSQL.AppendLine("                        hosp_data_type, m_mark");
            strSQL.AppendLine("                  from ICEI_3060_PBA_ORD");
            strSQL.AppendLine("                 where hosp_id        = :hospId");
            strSQL.AppendLine("                   and fee_ym         = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("                   and order_code     = :orderCode");
            strSQL.AppendLine("                   and hosp_data_type = :hospDataType");
            strSQL.AppendLine("                   and m_mark         = :mMark");
            strSQL.AppendLine("                   and (case when substr(hosp_data_type,1,1)='2' then '2' else '1' end) =:oipdType");
            strSQL.AppendLine("                   and i_code is not null");
            strSQL.AppendLine("                  group by branch_code,hosp_id,fee_ym,hosp_cnt_type,order_code,report_type,season_tot,mark_135,");
            strSQL.AppendLine("                           I_ASSAY_MARK,I_ASSAY_r_mark,I_ASSAY_r_hosp_id,");
            strSQL.AppendLine("                           case when substr(hosp_data_type,1,1)='2' then '2' else '1' end,");
            strSQL.AppendLine("                           hosp_data_type, m_mark");
            strSQL.AppendLine("            ) a");
            strSQL.AppendLine("      group by a.BRANCH_CODE,a.HOSP_ID,FEE_YM,");
            strSQL.AppendLine("                REPORT_TYPE,");
            strSQL.AppendLine("                case when I_ASSAY_MARK='1' and oipd_type='1' then '1'  -- 1-門診自行即時");
            strSQL.AppendLine("                     when I_ASSAY_MARK='1' and oipd_type='2' then '5'  -- 1-住診自行即時");
            strSQL.AppendLine("                     when I_ASSAY_MARK='2' and oipd_type='1' then '6'  -- 2-門診自行3日");
            strSQL.AppendLine("                     when I_ASSAY_MARK='2' and oipd_type='2' then '7'  -- 2-住診自行3日");
            strSQL.AppendLine("                     when I_ASSAY_R_MARK='3'  then '8'                 -- 3-交付即時");
            strSQL.AppendLine("                     when I_ASSAY_R_MARK='4'  then '9'                 -- 4-交付3日");
            strSQL.AppendLine("                     when I_ASSAY_MARK='5'  then 'C'                   -- 5-自行非即時");
            strSQL.AppendLine("                     when I_ASSAY_r_MARK='6'  then 'D'                 -- 6-交付非即時");
            strSQL.AppendLine("                     else 'E' end  ,                                   -- 7-未上傳");
            strSQL.AppendLine("                OIPD_TYPE,order_code,SEASON_TOT,HOSP_CNT_TYPE, MARK_135,");
            strSQL.AppendLine("                case when I_ASSAY_MARK='1' and oipd_type='1' then a.hosp_id  -- 1-門診自行即時");
            strSQL.AppendLine("                     when I_ASSAY_MARK='1' and oipd_type='2' then a.hosp_id  -- 1-住診自行即時");
            strSQL.AppendLine("                     when I_ASSAY_MARK='2' and oipd_type='1' then a.hosp_id  -- 2-門診自行3日");
            strSQL.AppendLine("                     when I_ASSAY_MARK='2' and oipd_type='2' then a.hosp_id  -- 2-住診自行3日");
            strSQL.AppendLine("                     when I_ASSAY_R_MARK='3'  then I_ASSAY_R_hosp_id         -- 3-交付即時");
            strSQL.AppendLine("                     when I_ASSAY_R_MARK='4'  then I_ASSAY_R_hosp_id         -- 4-交付3日");
            strSQL.AppendLine("                     when I_ASSAY_MARK='5'  then a.hosp_id                   -- 5-自行非即時");
            strSQL.AppendLine("                     when I_ASSAY_r_MARK='6'  then I_ASSAY_R_hosp_id         -- 6-交付非即時");
            strSQL.AppendLine("                     else 'XXXXXXXXXX' end,                                  -- 7-未上傳");
            strSQL.AppendLine("                hosp_data_type, m_mark");
            strSQL.AppendLine("       )");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                cmd.ExecuteNonQuery();
            }

            // 112/04/24先檢查是否有自行上傳INDEX=1,5的資料,若沒有要補入,其上傳數預設為0,
            // 以利分區業務組現行程式統計
            if (sql300.oipdType == "1")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        select count(*)
                          from ICEI_IAV_DTL_STS_FCAL
                         where hosp_id        = :hospId
                           and fee_ym         = to_date(:feeYm,'yyyymm')
                           and order_code     = :orderCode
                           and oipd_type      = :oipdType
                           and hosp_data_type = :hospDataType
                           and m_mark         = :mMark
                           and index_type     = '1'";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                    cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                    cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                    cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                    cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                    hIndex1Recd = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (hIndex1Recd == 0)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            insert into ICEI_IAV_DTL_STS_FCAL (
                                    BRANCH_CODE,HOSP_ID,FEE_YM,
                                    REPORT_TYPE,
                                    index_type,
                                    OIPD_TYPE,
                                    order_code,SEASON_TOT,HOSP_CNT_TYPE,
                                    APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,
                                    HOSP_DATA_TYPE, M_MARK, MONITOR_MARK)
                            values(:branchCode,:hospId,to_date(:feeYm,'yyyymm'),
                                    :reportType,
                                    '1',
                                    :oipdType,
                                    :orderCode,:seasonTot,:hospCntType,
                                    :applQty, 0,:mark135,:hospId,sysdate,
                                    :hospDataType, :mMark, 'Y')";
                        cmd.Parameters.Add(new OracleParameter("branchCode", sql300.branchCode));
                        cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                        cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                        cmd.Parameters.Add(new OracleParameter("reportType", sql300.reportType));
                        cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                        cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                        cmd.Parameters.Add(new OracleParameter("seasonTot", sql300.seasonTot));
                        cmd.Parameters.Add(new OracleParameter("hospCntType", sql300.hospCntType));
                        cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                        cmd.Parameters.Add(new OracleParameter("mark135", sql300.mark135));
                        cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                        cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            else if (sql300.oipdType == "2")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        select count(*)
                          from ICEI_IAV_DTL_STS_FCAL
                         where hosp_id        = :hospId
                           and fee_ym         = to_date(:feeYm,'yyyymm')
                           and order_code     = :orderCode
                           and oipd_type      = :oipdType
                           and hosp_data_type = :hospDataType
                           and m_mark         = :mMark
                           and index_type     = '5'";
                    cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                    cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                    cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                    cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                    cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                    hIndex5Recd = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (hIndex5Recd == 0)
                {
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            insert into ICEI_IAV_DTL_STS_FCAL (
                                    BRANCH_CODE,HOSP_ID,FEE_YM,
                                    REPORT_TYPE,
                                    index_type,
                                    OIPD_TYPE,
                                    order_code,SEASON_TOT,HOSP_CNT_TYPE,
                                    APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,
                                    HOSP_DATA_TYPE, M_MARK, MONITOR_MARK)
                            values(:branchCode,:hospId,to_date(:feeYm,'yyyymm'),
                                    :reportType,
                                    '5',
                                    :oipdType,
                                    :orderCode,:seasonTot,:hospCntType,
                                    :applQty, 0,:mark135,:hospId,sysdate,
                                    :hospDataType, :mMark, 'Y')";
                        cmd.Parameters.Add(new OracleParameter("branchCode", sql300.branchCode));
                        cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                        cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                        cmd.Parameters.Add(new OracleParameter("reportType", sql300.reportType));
                        cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                        cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                        cmd.Parameters.Add(new OracleParameter("seasonTot", sql300.seasonTot));
                        cmd.Parameters.Add(new OracleParameter("hospCntType", sql300.hospCntType));
                        cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                        cmd.Parameters.Add(new OracleParameter("mark135", sql300.mark135));
                        cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                        cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            // Insert index_type='2'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_IAV_DTL_STS_FCAL (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            '2' index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,hosp_id, sysdate TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, 'Y'");
            strSQL.AppendLine("       from ICEI_IAV_DTL_STS_FCAL");
            strSQL.AppendLine("      where hosp_id         = :hospId");
            strSQL.AppendLine("        and fee_ym          = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code      = :orderCode");
            strSQL.AppendLine("        and oipd_type       = :oipdType");
            strSQL.AppendLine("        and hosp_data_type  = :hospDataType");
            strSQL.AppendLine("        and m_mark          = :mMark");
            strSQL.AppendLine("        and index_type in ('1','5','6','7','C')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("             REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("             order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("             MARK_135,");
            strSQL.AppendLine("             HOSP_DATA_TYPE, M_MARK");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                cmd.ExecuteNonQuery();
            }

            // Insert index_type='A'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_IAV_DTL_STS_FCAL (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            'A' index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,'XXXXXXXXXX', sysdate TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, 'Y'");
            strSQL.AppendLine("       from ICEI_IAV_DTL_STS_FCAL");
            strSQL.AppendLine("      where hosp_id         = :hospId");
            strSQL.AppendLine("        and fee_ym          = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code      = :orderCode");
            strSQL.AppendLine("        and oipd_type       = :oipdType");
            strSQL.AppendLine("        and hosp_data_type  = :hospDataType");
            strSQL.AppendLine("        and m_mark          = :mMark");
            strSQL.AppendLine("        and  index_type in ('8','9','D')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("              REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("              order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("              MARK_135,");
            strSQL.AppendLine("              HOSP_DATA_TYPE, M_MARK");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                cmd.ExecuteNonQuery();
            }

            // Insert index_type='B'
            strSQL.Clear();
            strSQL.AppendLine("insert into ICEI_IAV_DTL_STS_FCAL (");
            strSQL.AppendLine("            BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,");
            strSQL.AppendLine("            index_type,");
            strSQL.AppendLine("            OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            APPL_QTY,audit_qty,MARK_135,REAL_HOSP_ID,TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, MONITOR_MARK )");
            strSQL.AppendLine("     select BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("            REPORT_TYPE,'B' index_type,OIPD_TYPE,");
            strSQL.AppendLine("            order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("            :applQty,sum(audit_qty),MARK_135,'XXXXXXXXXX', sysdate TXT_DATE,");
            strSQL.AppendLine("            HOSP_DATA_TYPE, M_MARK, 'Y'");
            strSQL.AppendLine("       from ICEI_IAV_DTL_STS_FCAL");
            strSQL.AppendLine("      where hosp_id         = :hospId");
            strSQL.AppendLine("        and fee_ym          = to_date(:feeYm,'yyyymm')");
            strSQL.AppendLine("        and order_code      = :orderCode");
            strSQL.AppendLine("        and oipd_type       = :oipdType");
            strSQL.AppendLine("        and hosp_data_type  = :hospDataType");
            strSQL.AppendLine("        and m_mark          = :mMark");
            strSQL.AppendLine("        and  index_type in ('1','5','6','7','C','8','9','D')");
            strSQL.AppendLine("     group by BRANCH_CODE,HOSP_ID,FEE_YM,");
            strSQL.AppendLine("              REPORT_TYPE,OIPD_TYPE,");
            strSQL.AppendLine("              order_code,SEASON_TOT,HOSP_CNT_TYPE,");
            strSQL.AppendLine("              MARK_135,");
            strSQL.AppendLine("              HOSP_DATA_TYPE, M_MARK");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = strSQL.ToString();
                cmd.Parameters.Add(new OracleParameter("applQty", sql300.applQty));
                cmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                cmd.Parameters.Add(new OracleParameter("orderCode", sql300.orderCode));
                cmd.Parameters.Add(new OracleParameter("oipdType", sql300.oipdType));
                cmd.Parameters.Add(new OracleParameter("hospDataType", sql300.hospDataType));
                cmd.Parameters.Add(new OracleParameter("mMark", sql300.mMark));
                cmd.ExecuteNonQuery();
            }
        }

        // Original: ins_op_mst
        private static void InsOpMst(string tableName, string indexType, string branchCode, string hospId, string feeYm, int seasonTot, string hospCntType)
        {
            _logger.Info($"InsOpMst tableName={tableName} indexType={indexType} hospId={hospId} feeYm={feeYm}");

            int hospOpApplQty = 0;
            int hospOp3dayQty = 0;
            int sqlRecCount = 0;

            ExecuteNonQuery($@"
                DELETE {tableName}
                 WHERE HOSP_ID = '{hospId}'
                   AND FEE_YM  = TO_DATE('{feeYm}','YYYYMMDD')");

            if (tableName == "ICEI_ASSAY_DL1_MST")
            {
                // 即時上傳檢驗(查)結果門診額外獎勵金
                ExecuteNonQuery($@"
                    INSERT INTO {tableName} (
                           BRANCH_CODE,
                           HOSP_ID,
                           FEE_YM,
                           REPORT_TYPE,
                           SEASON_TOT,
                           INDEX_TYPE,
                           HOSP_CNT_TYPE,
                           APPL_QTY,
                           AUDIT_QTY,
                           MARK_135,
                           AUDIT_3_QTY,
                           TXT_DATE,
                           PROC_STATUS )
                    SELECT BRANCH_CODE,
                           HOSP_ID,
                           FEE_YM,
                           REPORT_TYPE,
                           SEASON_TOT,
                           '{indexType}',
                           HOSP_CNT_TYPE,
                           {hospOpApplQty},
                           SUM(nvl(AUDIT_QTY,0)),
                           MARK_135,
                           {hospOp3dayQty},
                           SYSDATE,
                           'Y'
                      FROM ICEI_ASSAY_DTL_STS
                     WHERE branch_code='{branchCode}'
                       and FEE_YM     = TO_DATE('{feeYm}','YYYYMMDD')
                       AND HOSP_ID    = '{hospId}'  and index_type='1'
                    GROUP BY BRANCH_CODE, HOSP_ID, FEE_YM, REPORT_TYPE, SEASON_TOT, INDEX_TYPE, HOSP_CNT_TYPE, MARK_135", out sqlRecCount);

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        update icei_assay_dl1_mst m
                           set appl_qty= (select sum(appl_qty)
                                            from icei_3060_ord_sts x
                                           where x.hosp_id=:hospId
                                             and x.fee_ym=to_date(:feeYm,'yyyymmdd')
                                             and x.sts_type='A'
                                             and x.hosp_id=m.hosp_id
                                             and x.fee_ym=m.fee_ym
                                             and x.report_type=m.report_type
                                             and x.mark_135=m.mark_135
                                             and x.oipd_type='1'
                                           )
                       where m.hosp_id=:hospId
                         and m.fee_ym=to_date(:feeYm,'yyyymmdd')";
                    cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", feeYm));
                    cmd.ExecuteNonQuery();
                }

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        update icei_assay_dl1_mst m
                           set AUDIT_3_QTY= (select sum(nvl(REAL_QTY_3DAY,0))
                                            from icei_3060_ord_sts x
                                           where x.hosp_id=:hospId
                                             and x.fee_ym=to_date(:feeYm,'yyyymmdd')
                                             and x.sts_type='A'
                                             and x.hosp_id=m.hosp_id
                                             and x.fee_ym=m.fee_ym
                                             and x.report_type=m.report_type
                                             and x.mark_135=m.mark_135
                                             and x.oipd_type='1'
                                           )
                       where m.hosp_id=:hospId
                         and m.fee_ym=to_date(:feeYm,'yyyymmdd')";
                    cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", feeYm));
                    cmd.ExecuteNonQuery();
                }
            }
            else if (tableName == "ICEI_ASSAY_DL8_MST")
            {
                // 三日上傳檢驗(查)結果門診額外獎勵金
                ExecuteNonQuery($@"
                    INSERT INTO {tableName} (
                           BRANCH_CODE,
                           HOSP_ID,
                           FEE_YM,
                           REPORT_TYPE,
                           SEASON_TOT,
                           INDEX_TYPE,
                           HOSP_CNT_TYPE,
                           APPL_QTY,
                           AUDIT_QTY,
                           MARK_135,
                           TXT_DATE,
                           PROC_STATUS )
                    SELECT BRANCH_CODE,
                           HOSP_ID,
                           FEE_YM,
                           REPORT_TYPE,
                           SEASON_TOT,
                           '{indexType}',
                           HOSP_CNT_TYPE,
                           {hospOpApplQty},
                           SUM(nvl(audit_qty,0)),
                           MARK_135,
                           SYSDATE,
                           'Y'
                      FROM ICEI_ASSAY_DTL_STS
                     WHERE branch_code='{branchCode}'
                       and FEE_YM     = TO_DATE('{feeYm}','YYYYMMDD')
                       AND HOSP_ID    = '{hospId}' and index_type='6'
                    GROUP BY BRANCH_CODE, HOSP_ID, FEE_YM, REPORT_TYPE, SEASON_TOT, INDEX_TYPE, HOSP_CNT_TYPE, MARK_135", out sqlRecCount);

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        update icei_assay_dl8_mst m
                           set appl_qty= (select sum(appl_qty)
                                            from icei_3060_ord_sts x
                                           where x.hosp_id=:hospId
                                             and x.fee_ym=to_date(:feeYm,'yyyymmdd')
                                             and x.sts_type='A'
                                             and x.hosp_id=m.hosp_id
                                             and x.fee_ym=m.fee_ym
                                             and x.report_type=m.report_type
                                             and x.mark_135=m.mark_135
                                             and x.oipd_type='1'
                                           )
                       where m.hosp_id=:hospId
                         and m.fee_ym=to_date(:feeYm,'yyyymmdd')";
                    cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", feeYm));
                    cmd.ExecuteNonQuery();
                }
            }

            if (sqlRecCount == 0)
            {
                ExecuteNonQuery($@"
                    insert into {tableName}
                           ( branch_code,
                             hosp_id,
                             season_tot,
                             fee_ym,
                             index_type,
                             hosp_cnt_type,
                             report_type,
                             mark_135,
                             appl_qty,
                             assay_qty,
                             audit_qty,
                             txt_date,
                             PROC_STATUS )
                      select '{branchCode}' branch_code, --wk_branch_code
                             '{hospId}' hosp_id,    --wk_hosp_id
                             {seasonTot}   season_tot, --wk_season_tot
                             to_date('{feeYm}','yyyymmdd') fee_ym,  --wk_fee_ym
                             '{indexType}' index_type,   --wk_index_type
                             '{hospCntType}' hosp_cnt_type, --wk_real_hosp_cnt_type
                             1 report_type,
                             'N' mark_135,
                             0 appl_qty,
                             0 assay_qty,
                             0 audit_qty,
                             sysdate txt_date,
                             'Y' PROC_STATUS
                        from dual");
            }
        }

        // Original: ins_hp_mst
        private static void InsHpMst(string tableName, string indexType, string branchCode, string hospId, string feeYm, int seasonTot, string hospCntType)
        {
            _logger.Info($"InsHpMst tableName={tableName} indexType={indexType} hospId={hospId} feeYm={feeYm}");

            int hospHpApplQty = 0;
            int hospHp3dayQty = 0;
            int sqlRecCount = 0;

            ExecuteNonQuery($@"
                DELETE {tableName}
                 WHERE HOSP_ID = '{hospId}'
                   AND FEE_YM  = TO_DATE('{feeYm}','YYYYMMDD')");

            if (tableName == "ICEI_ASSAY_DL5_MST")
            {
                // 即時上傳檢驗(查)結果住診額外獎勵金
                ExecuteNonQuery($@"
                    INSERT INTO {tableName} (
                           BRANCH_CODE,
                           HOSP_ID,
                           FEE_YM,
                           REPORT_TYPE,
                           SEASON_TOT,
                           INDEX_TYPE,
                           HOSP_CNT_TYPE,
                           APPL_QTY,
                           AUDIT_QTY,
                           MARK_135,
                           AUDIT_3_QTY,
                           TXT_DATE,
                           PROC_STATUS )
                    SELECT BRANCH_CODE,
                           HOSP_ID,
                           FEE_YM,
                           REPORT_TYPE,
                           SEASON_TOT,
                           '{indexType}',
                           HOSP_CNT_TYPE,
                           {hospHpApplQty},
                           SUM(nvl(AUDIT_QTY,0)),
                           MARK_135,
                           {hospHp3dayQty},
                           SYSDATE,
                           'Y'
                      FROM ICEI_ASSAY_DTL_STS
                     WHERE branch_code='{branchCode}'
                       and FEE_YM     = TO_DATE('{feeYm}','YYYYMMDD')
                       AND HOSP_ID    = '{hospId}'  and index_type='5'
                    GROUP BY BRANCH_CODE, HOSP_ID, FEE_YM, REPORT_TYPE, SEASON_TOT, INDEX_TYPE, HOSP_CNT_TYPE, MARK_135", out sqlRecCount);

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        update icei_assay_dl5_mst m
                           set appl_qty= (select sum(appl_qty)
                                            from icei_3060_ord_sts x
                                           where x.hosp_id=:hospId
                                             and x.fee_ym=to_date(:feeYm,'yyyymmdd')
                                             and x.sts_type='A'
                                             and x.hosp_id=m.hosp_id
                                             and x.fee_ym=m.fee_ym
                                             and x.report_type=m.report_type
                                             and x.mark_135=m.mark_135
                                             and x.oipd_type='2'
                                           )
                       where m.hosp_id=:hospId
                         and m.fee_ym=to_date(:feeYm,'yyyymmdd')";
                    cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", feeYm));
                    cmd.ExecuteNonQuery();
                }

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        update icei_assay_dl5_mst m
                           set AUDIT_3_QTY= (select sum(nvl(REAL_QTY_3DAY,0))
                                            from icei_3060_ord_sts x
                                           where x.hosp_id=:hospId
                                             and x.fee_ym=to_date(:feeYm,'yyyymmdd')
                                             and x.sts_type='A'
                                             and x.hosp_id=m.hosp_id
                                             and x.fee_ym=m.fee_ym
                                             and x.report_type=m.report_type
                                             and x.mark_135=m.mark_135
                                             and x.oipd_type='2'
                                           )
                       where m.hosp_id=:hospId
                         and m.fee_ym=to_date(:feeYm,'yyyymmdd')";
                    cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", feeYm));
                    cmd.ExecuteNonQuery();
                }
            }
            else if (tableName == "ICEI_ASSAY_DL9_MST")
            {
                // 三日上傳檢驗(查)結果住診額外獎勵金
                ExecuteNonQuery($@"
                    INSERT INTO {tableName} (
                           BRANCH_CODE,
                           HOSP_ID,
                           FEE_YM,
                           REPORT_TYPE,
                           SEASON_TOT,
                           INDEX_TYPE,
                           HOSP_CNT_TYPE,
                           APPL_QTY,
                           AUDIT_QTY,
                           MARK_135,
                           TXT_DATE,
                           PROC_STATUS )
                    SELECT BRANCH_CODE,
                           HOSP_ID,
                           FEE_YM,
                           REPORT_TYPE,
                           SEASON_TOT,
                           '{indexType}',
                           HOSP_CNT_TYPE,
                           {hospHpApplQty},
                           SUM(nvl(AUDIT_QTY,0)),
                           MARK_135,
                           SYSDATE,
                           'Y'
                      FROM ICEI_ASSAY_DTL_STS
                     WHERE branch_code='{branchCode}'
                       and FEE_YM     = TO_DATE('{feeYm}','YYYYMMDD')
                       AND HOSP_ID    = '{hospId}'  and index_type='7'
                    GROUP BY BRANCH_CODE, HOSP_ID, FEE_YM, REPORT_TYPE, SEASON_TOT, INDEX_TYPE, HOSP_CNT_TYPE, MARK_135", out sqlRecCount);

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = @"
                        update icei_assay_dl9_mst m
                           set appl_qty= (select sum(appl_qty)
                                            from icei_3060_ord_sts x
                                           where x.hosp_id=:hospId
                                             and x.fee_ym=to_date(:feeYm,'yyyymmdd')
                                             and x.sts_type='A'
                                             and x.hosp_id=m.hosp_id
                                             and x.fee_ym=m.fee_ym
                                             and x.report_type=m.report_type
                                             and x.mark_135=m.mark_135
                                             and x.oipd_type='2'
                                           )
                       where m.hosp_id=:hospId
                         and m.fee_ym=to_date(:feeYm,'yyyymmdd')";
                    cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                    cmd.Parameters.Add(new OracleParameter("feeYm", feeYm));
                    cmd.ExecuteNonQuery();
                }
            }

            if (sqlRecCount == 0)
            {
                ExecuteNonQuery($@"
                    insert into {tableName}
                           ( branch_code,
                             hosp_id,
                             season_tot,
                             fee_ym,
                             index_type,
                             hosp_cnt_type,
                             report_type,
                             mark_135,
                             appl_qty,
                             assay_qty,
                             audit_qty,
                             txt_date,
                             PROC_STATUS )
                      select '{branchCode}' branch_code, --wk_branch_code
                             '{hospId}' hosp_id,    --wk_hosp_id
                             {seasonTot}   season_tot, --wk_season_tot
                             to_date('{feeYm}','yyyymmdd') fee_ym,  --wk_fee_ym
                             '{indexType}' index_type,   --wk_index_type
                             '{hospCntType}' hosp_cnt_type, --wk_real_hosp_cnt_type
                             1 report_type,
                             'N' mark_135,
                             0 appl_qty,
                             0 assay_qty,
                             0 audit_qty,
                             sysdate txt_date,
                             'Y' PROC_STATUS
                        from dual");
            }
        }

        // Original: ins_ophp_mst
        private static void InsOphpMst(string tableName, string indexType, string branchCode, string hospId, string feeYm, int seasonTot, string hospCntType)
        {
            _logger.Info($"InsOphpMst tableName={tableName} indexType={indexType} hospId={hospId} feeYm={feeYm}");

            int hospOpApplQty = 0;
            int hospHpApplQty = 0;
            int sqlRecCount = 0;

            ExecuteNonQuery($@"
                DELETE {tableName}
                 WHERE HOSP_ID = '{hospId}'
                   AND FEE_YM  = TO_DATE('{feeYm}','YYYYMMDD')");

            // 檢驗(查)結果(自行即時＋自行三日＋自行逾三日)
            ExecuteNonQuery($@"
                INSERT INTO {tableName} (
                       BRANCH_CODE,
                       HOSP_ID,
                       FEE_YM,
                       REPORT_TYPE,
                       SEASON_TOT,
                       INDEX_TYPE,
                       HOSP_CNT_TYPE,
                       APPL_QTY,
                       AUDIT_QTY,
                       MARK_135,
                       TXT_DATE,
                       PROC_STATUS )
                SELECT BRANCH_CODE,
                       HOSP_ID,
                       FEE_YM,
                       REPORT_TYPE,
                       SEASON_TOT,
                       '{indexType}',
                       HOSP_CNT_TYPE,
                       {hospOpApplQty + hospHpApplQty},
                       sum(audit_qty),
                       MARK_135,
                       SYSDATE,
                       'Y'
                  FROM ICEI_ASSAY_DTL_STS
                 WHERE branch_code='{branchCode}'
                   and FEE_YM     = TO_DATE('{feeYm}','YYYYMMDD')
                   AND HOSP_ID    = '{hospId}' and index_type='2'
                GROUP BY BRANCH_CODE, HOSP_ID, FEE_YM, REPORT_TYPE, SEASON_TOT, INDEX_TYPE, HOSP_CNT_TYPE, MARK_135", out sqlRecCount);

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = @"
                    update icei_assay_dl2_mst m
                       set appl_qty= (select sum(appl_qty)
                                        from icei_3060_ord_sts x
                                       where x.hosp_id=:hospId
                                         and x.fee_ym=to_date(:feeYm,'yyyymmdd')
                                         and x.sts_type='A'
                                         and x.hosp_id=m.hosp_id
                                         and x.fee_ym=m.fee_ym
                                         and x.report_type=m.report_type
                                         and x.mark_135=m.mark_135
                                       )
                   where m.hosp_id=:hospId
                     and m.fee_ym=to_date(:feeYm,'yyyymmdd')";
                cmd.Parameters.Add(new OracleParameter("hospId", hospId));
                cmd.Parameters.Add(new OracleParameter("feeYm", feeYm));
                cmd.ExecuteNonQuery();
            }

            if (sqlRecCount == 0)
            {
                ExecuteNonQuery($@"
                    insert into {tableName}
                           ( branch_code,
                             hosp_id,
                             season_tot,
                             fee_ym,
                             index_type,
                             hosp_cnt_type,
                             report_type,
                             mark_135,
                             appl_qty,
                             assay_qty,
                             audit_qty,
                             txt_date,
                             PROC_STATUS )
                      select '{branchCode}' branch_code, --wk_branch_code
                             '{hospId}' hosp_id,    --wk_hosp_id
                             {seasonTot}   season_tot, --wk_season_tot
                             to_date('{feeYm}','yyyymmdd') fee_ym,  --wk_fee_ym
                             '{indexType}' index_type,   --wk_index_type
                             '{hospCntType}' hosp_cnt_type, --wk_real_hosp_cnt_type
                             1 report_type,
                             'N' mark_135,
                             0 appl_qty,
                             0 assay_qty,
                             0 audit_qty,
                             sysdate txt_date,
                             'Y' PROC_STATUS
                        from dual");
            }
        }

        // Original: ins_op_r_mst
        private static void InsOpRMst(string tableName, string indexType, string branchCode, string hospId, string feeYm, int seasonTot, string hospCntType)
        {
            string columnAudit = string.Empty;
            string dtlStsIndexType = string.Empty;
            string columnCondition = string.Empty;
            string oipdType = "1";
            int sqlRecCount = 0;

            if (tableName == "ICEI_ASSAY_DLA_MST")
            {
                columnAudit = "AUDIT_QTY";
                dtlStsIndexType = "8";
                columnCondition = "ASSAY_R_HOSP_ID";
            }
            else if (tableName == "ICEI_ASSAY_DLB_MST")
            {
                columnAudit = "AUDIT_QTY";
                dtlStsIndexType = "9";
                columnCondition = "ASSAY_3_R_HOSP_ID";
            }

            _logger.Info($"InsOpRMst tableName={tableName} indexType={indexType} hospId={hospId} feeYm={feeYm}");

            ExecuteNonQuery($@"
                DELETE {tableName}
                WHERE HOSP_ID = '{hospId}'
                  AND FEE_YM  = TO_DATE('{feeYm}','YYYYMMDD')
                  AND OIPD_TYPE = '{oipdType}'");

            // 交付即時上傳檢驗(查)結果門診、住診額外獎勵金 ICEI_ASSAY_DTA_MST  INDEX_TYPE＝8
            // 交付三日上傳檢驗(查)結果門診、住診額外獎勵金 ICEI_ASSAY_DTB_MST  INDEX_TYPE＝9
            ExecuteNonQuery($@"
                INSERT INTO {tableName} (
                       BRANCH_CODE,
                       HOSP_ID,
                       FEE_YM,
                       REPORT_TYPE,
                       SEASON_TOT,
                       INDEX_TYPE,
                       HOSP_CNT_TYPE,
                       APPL_QTY,
                       audit_qty,
                       MARK_135,
                       REAL_HOSP_ID,
                       OIPD_TYPE,
                       TXT_DATE,
                       PROC_STATUS )
                SELECT BRANCH_CODE,
                       HOSP_ID,
                       FEE_YM,
                       REPORT_TYPE,
                       SEASON_TOT,
                       '{indexType}',
                       HOSP_CNT_TYPE,
                       SUM(audit_qty),
                       SUM(audit_qty),
                       MARK_135,
                       real_hosp_id,
                       '{oipdType}',
                       SYSDATE,
                       'Y'
                  FROM ICEI_ASSAY_DTL_STS
                 WHERE branch_code='{branchCode}'
                   and FEE_YM     = TO_DATE('{feeYm}','YYYYMMDD')
                   AND HOSP_ID    = '{hospId}'  and index_type='{dtlStsIndexType}'  and oipd_type='{oipdType}'
                 GROUP BY BRANCH_CODE, HOSP_ID, FEE_YM, REPORT_TYPE, SEASON_TOT, INDEX_TYPE, HOSP_CNT_TYPE, MARK_135,real_hosp_id", out sqlRecCount);

            if (sqlRecCount == 0)
            {
                ExecuteNonQuery($@"
                    insert into {tableName}
                           ( branch_code,
                             hosp_id,
                             season_tot,
                             fee_ym,
                             index_type,
                             hosp_cnt_type,
                             report_type,
                             mark_135,
                             real_hosp_id,
                             appl_qty,
                             assay_qty,
                             audit_qty,
                             txt_date,
                             PROC_STATUS,oipd_type)
                      select '{branchCode}' branch_code, --wk_branch_code
                             '{hospId}' hosp_id,    --wk_hosp_id
                             {seasonTot}   season_tot, --wk_season_tot
                             to_date('{feeYm}','yyyymmdd') fee_ym,  --wk_fee_ym
                             '{indexType}' index_type,   --wk_index_type
                             '{hospCntType}' hosp_cnt_type, --wk_real_hosp_cnt_type
                             1 report_type,
                             'N' mark_135,
                             'X' real_hosp_id,
                             0 appl_qty,
                             0 assay_qty,
                             0 audit_qty,
                             sysdate txt_date,
                             'Y' PROC_STATUS,'1'
                        from dual");
            }
        }

        // Original: ins_hp_r_mst
        private static void InsHpRMst(string tableName, string indexType, string branchCode, string hospId, string feeYm, int seasonTot, string hospCntType)
        {
            string columnAudit = string.Empty;
            string dtlStsIndexType = string.Empty;
            string columnCondition = string.Empty;
            string oipdType = "2";
            int sqlRecCount = 0;

            if (tableName == "ICEI_ASSAY_DLA_MST")
            {
                columnAudit = "AUDIT_QTY";
                dtlStsIndexType = "8";
                columnCondition = "ASSAY_R_HOSP_ID";
            }
            else if (tableName == "ICEI_ASSAY_DLB_MST")
            {
                columnAudit = "AUDIT_QTY";
                dtlStsIndexType = "9";
                columnCondition = "ASSAY_3_R_HOSP_ID";
            }

            _logger.Info($"InsHpRMst tableName={tableName} indexType={indexType} hospId={hospId} feeYm={feeYm}");

            ExecuteNonQuery($@"
                DELETE {tableName}
                WHERE HOSP_ID = '{hospId}'
                  AND FEE_YM  = TO_DATE('{feeYm}','YYYYMMDD')
                  AND OIPD_TYPE = '{oipdType}'");

            ExecuteNonQuery($@"
                INSERT INTO {tableName} (
                       BRANCH_CODE,
                       HOSP_ID,
                       FEE_YM,
                       REPORT_TYPE,
                       SEASON_TOT,
                       INDEX_TYPE,
                       HOSP_CNT_TYPE,
                       APPL_QTY,
                       audit_qty,
                       MARK_135,
                       REAL_HOSP_ID,
                       OIPD_TYPE,
                       TXT_DATE,
                       PROC_STATUS )
                SELECT BRANCH_CODE,
                       HOSP_ID,
                       FEE_YM,
                       REPORT_TYPE,
                       SEASON_TOT,
                       '{indexType}',
                       HOSP_CNT_TYPE,
                       SUM(audit_qty),
                       SUM(audit_qty),
                       MARK_135,
                       real_hosp_id,
                       '{oipdType}',
                       SYSDATE,
                       'Y'
                  FROM ICEI_ASSAY_DTL_STS
                 WHERE branch_code='{branchCode}'
                   and FEE_YM     = TO_DATE('{feeYm}','YYYYMMDD')
                   AND HOSP_ID    = '{hospId}'  and index_type='{dtlStsIndexType}' and oipd_type='{oipdType}'
                 GROUP BY BRANCH_CODE, HOSP_ID, FEE_YM, REPORT_TYPE, SEASON_TOT, INDEX_TYPE, HOSP_CNT_TYPE, MARK_135,real_hosp_id", out sqlRecCount);

            if (sqlRecCount == 0)
            {
                ExecuteNonQuery($@"
                    insert into {tableName}
                           ( branch_code,
                             hosp_id,
                             season_tot,
                             fee_ym,
                             index_type,
                             hosp_cnt_type,
                             report_type,
                             mark_135,
                             real_hosp_id,
                             appl_qty,
                             assay_qty,
                             audit_qty,
                             txt_date,
                             PROC_STATUS,oipd_type)
                      select '{branchCode}' branch_code, --wk_branch_code
                             '{hospId}' hosp_id,    --wk_hosp_id
                             {seasonTot}   season_tot, --wk_season_tot
                             to_date('{feeYm}','yyyymmdd') fee_ym,  --wk_fee_ym
                             '{indexType}' index_type,   --wk_index_type
                             '{hospCntType}' hosp_cnt_type, --wk_real_hosp_cnt_type
                             1 report_type,
                             'N' mark_135,
                             'X' real_hosp_id,
                             0 appl_qty,
                             0 assay_qty,
                             0 audit_qty,
                             sysdate txt_date,
                             'Y' PROC_STATUS,'2'
                        from dual");
            }
        }

        // Original: upd_monitor_mark
        private static void UpdateMonitorMark(string branchCode, string hospId, string feeYm)
        {
            ExecuteNonQuery($@"
                UPDATE ICEI_ASSAY_DTL_STS A
                   SET MONITOR_MARK = CASE WHEN A.ORDER_CODE IN ('18005C','18006C','18007C','18033B','18041B','19001C',
                                                                 '19003C','19005C','19009C','19010C','19012C','19014C',
                                                                 '19015C','19016C','19018C','20013C','20026B','21008C',
                                                                 '28016C','28017C') THEN 'Y'
                                           ELSE NVL( ( SELECT 'Y'
                                                         FROM ICEI_DAY_REPORT_CODE B
                                                        WHERE SUBSTR(A.ORDER_CODE,1,5) = SUBSTR(B.ORDER_CODE,1,5)
                                                          AND A.FEE_YM BETWEEN B.MONITOR_B_DATE AND B.MONITOR_E_DATE
                                                          AND A.FEE_YM BETWEEN B.VALID_S_DATE AND B.VALID_E_DATE ), 'N') END
                 WHERE BRANCH_CODE = '{branchCode}'
                   AND HOSP_ID     = '{hospId}'
                   AND FEE_YM      = TO_DATE('{feeYm}','YYYYMM')");

            ExecuteNonQuery($@"
                UPDATE ICEI_ASSAY_DTL_STS_FCAL A
                   SET MONITOR_MARK = CASE WHEN A.ORDER_CODE IN ('18005C','18006C','18007C','18033B','18041B','19001C',
                                                                 '19003C','19005C','19009C','19010C','19012C','19014C',
                                                                 '19015C','19016C','19018C','20013C','20026B','21008C',
                                                                 '28016C','28017C') THEN 'Y'
                                           ELSE NVL( ( SELECT 'Y'
                                                         FROM ICEI_DAY_REPORT_CODE B
                                                        WHERE SUBSTR(A.ORDER_CODE,1,5) = SUBSTR(B.ORDER_CODE,1,5)
                                                          AND A.FEE_YM BETWEEN B.MONITOR_B_DATE AND B.MONITOR_E_DATE
                                                          AND A.FEE_YM BETWEEN B.VALID_S_DATE AND B.VALID_E_DATE ), 'N') END
                 WHERE BRANCH_CODE = '{branchCode}'
                   AND HOSP_ID     = '{hospId}'
                   AND FEE_YM      = TO_DATE('{feeYm}','YYYYMM')");

            ExecuteNonQuery($@"
                UPDATE ICEI_3060_ORD_STS A
                   SET MONITOR_MARK = CASE WHEN A.STS_TYPE = 'A' AND A.ORDER_CODE IN ('18005C','18006C','18007C','18033B','18041B','19001C',
                                                                                      '19003C','19005C','19009C','19010C','19012C','19014C',
                                                                                      '19015C','19016C','19018C','20013C','20026B','21008C',
                                                                                      '28016C','28017C') THEN 'Y'
                                           WHEN A.STS_TYPE = 'I' AND A.ORDER_CODE IN ('18005C','18006C','18007C','18033B','18041B','19001C',
                                                                                      '19003C','19005C','19009C','19010C','19012C','19014C',
                                                                                      '19015C','19016C','19018C','20013C','20026B','21008C',
                                                                                      '28016C','28017C') THEN 'N'
                                           ELSE NVL( ( SELECT 'Y'
                                                         FROM ICEI_DAY_REPORT_CODE B
                                                        WHERE SUBSTR(A.ORDER_CODE,1,5) = SUBSTR(B.ORDER_CODE,1,5)
                                                          AND A.FEE_YM BETWEEN B.MONITOR_B_DATE AND B.MONITOR_E_DATE
                                                          AND A.FEE_YM BETWEEN B.VALID_S_DATE AND B.VALID_E_DATE ), 'N') END
                 WHERE BRANCH_CODE = '{branchCode}'
                   AND HOSP_ID     = '{hospId}'
                   AND FEE_YM      = TO_DATE('{feeYm}','YYYYMM')");

            ExecuteNonQuery($@"
                UPDATE ICEI_3060_ORD_STS_FCAL A
                   SET MONITOR_MARK = CASE WHEN A.STS_TYPE = 'A' AND A.ORDER_CODE IN ('18005C','18006C','18007C','18033B','18041B','19001C',
                                                                                      '19003C','19005C','19009C','19010C','19012C','19014C',
                                                                                      '19015C','19016C','19018C','20013C','20026B','21008C',
                                                                                      '28016C','28017C') THEN 'Y'
                                           WHEN A.STS_TYPE = 'I' AND A.ORDER_CODE IN ('18005C','18006C','18007C','18033B','18041B','19001C',
                                                                                      '19003C','19005C','19009C','19010C','19012C','19014C',
                                                                                      '19015C','19016C','19018C','20013C','20026B','21008C',
                                                                                      '28016C','28017C') THEN 'N'
                                           ELSE NVL( ( SELECT 'Y'
                                                         FROM ICEI_DAY_REPORT_CODE B
                                                        WHERE SUBSTR(A.ORDER_CODE,1,5) = SUBSTR(B.ORDER_CODE,1,5)
                                                          AND A.FEE_YM BETWEEN B.MONITOR_B_DATE AND B.MONITOR_E_DATE
                                                          AND A.FEE_YM BETWEEN B.VALID_S_DATE AND B.VALID_E_DATE ), 'N') END
                 WHERE BRANCH_CODE = '{branchCode}'
                   AND HOSP_ID     = '{hospId}'
                   AND FEE_YM      = TO_DATE('{feeYm}','YYYYMM')");

            ExecuteNonQuery($@"
                UPDATE ICEI_IAV_DTL_STS A
                   SET MONITOR_MARK =  NVL( ( SELECT 'Y'
                                                FROM ICEI_DAY_REPORT_CODE B
                                               WHERE SUBSTR(A.ORDER_CODE,1,5) = SUBSTR(B.ORDER_CODE,1,5)
                                                 AND A.FEE_YM BETWEEN B.MONITOR_B_DATE AND B.MONITOR_E_DATE
                                                 AND A.FEE_YM BETWEEN B.VALID_S_DATE AND B.VALID_E_DATE ), 'N')
                 WHERE BRANCH_CODE = '{branchCode}'
                   AND HOSP_ID     = '{hospId}'
                   AND FEE_YM      = TO_DATE('{feeYm}','YYYYMM')");

            ExecuteNonQuery($@"
                UPDATE ICEI_IAV_DTL_STS_FCAL A
                   SET MONITOR_MARK =  NVL( ( SELECT 'Y'
                                                FROM ICEI_DAY_REPORT_CODE B
                                               WHERE SUBSTR(A.ORDER_CODE,1,5) = SUBSTR(B.ORDER_CODE,1,5)
                                                 AND A.FEE_YM BETWEEN B.MONITOR_B_DATE AND B.MONITOR_E_DATE
                                                 AND A.FEE_YM BETWEEN B.VALID_S_DATE AND B.VALID_E_DATE ), 'N')
                 WHERE BRANCH_CODE = '{branchCode}'
                   AND HOSP_ID     = '{hospId}'
                   AND FEE_YM      = TO_DATE('{feeYm}','YYYYMM')");

            ExecuteNonQuery("COMMIT");
        }

        // Helper method to execute non-query SQL statements
        private static void ExecuteNonQuery(string sql, out int rowsAffected)
        {
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = sql;
                rowsAffected = cmd.ExecuteNonQuery();
            }
        }

        private static void ExecuteNonQuery(string sql)
        {
            int rowsAffected;
            ExecuteNonQuery(sql, out rowsAffected);
        }
        #endregion
    }
}
```