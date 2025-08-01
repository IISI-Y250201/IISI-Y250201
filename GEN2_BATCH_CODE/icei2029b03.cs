```csharp
/*******************************************************************
程式代碼：icei2029b03
程式名稱：24小時上傳註記勾稽作業
功能簡述：更新RAPI_ASSAY_DATA_FINAL表中的24小時上傳註記
參    數：
參數一：程式代號 執行類別 費用年月 醫事機構代碼(選項) 有效迄日 更新註記 分區別(選項)
範例一：icei2029b03 0 20190101 ALL
讀取檔案：無
異動檔案：無
作    者：系統管理員
歷次修改時間：
1.20230101
需求單號暨修改內容簡述：
1.轉換為C#版本
備    註：
********************************************************************/

using System;
using System.Data;
using System.Text;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei2029b03
{
    public class icei2029b03
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
        private static string _sInputFeeYm = string.Empty;
        private static string _sInputHospId = string.Empty;
        private static string _sValidSDate = string.Empty;
        private static string _sMark24hr = string.Empty;
        private static string _wkBranchCode = string.Empty;
        private static string _sExecFlag = string.Empty;
        private static int _iExeType = 1;

        /* ---------- SQL structures ---------- */
        private class SQL100
        {
            public string hospId { get; set; } = string.Empty;
            public string recvSDate { get; set; } = string.Empty;
            public string recvEDate { get; set; } = string.Empty;
            public string feeSDate { get; set; } = string.Empty;
            public string feeEDate { get; set; } = string.Empty;
        }

        /* ---------- Main ---------- */
        static void Main(string[] args)
        {
            try
            {
                // PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                Console.WriteLine("========== icei2029b03 start ========== ");

                CheckArg(args);

                if (_sExecFlag == "0")
                {
                    UpdateMark24Hr();
                }

                if (_sExecFlag == "1")
                {
                    UpdateMark560();
                }

                if (_sExecFlag == "2")
                {
                    UpdateMark24hrPBA560();
                }

                if (_sExecFlag == "3")
                {
                    UpdateMark24hrHPA();
                }

                if (_sExecFlag == "7")
                {
                    UpdateMark24hrHPAA();
                }

                if (_sExecFlag == "4")
                {
                    UpdateMark24hrHPB();
                }

                if (_sExecFlag == "8")
                {
                    UpdateMark24hrHPBB();
                }

                if (_sExecFlag == "5")
                {
                    UpdateMark24hrHPC();
                }

                if (_sExecFlag == "9")
                {
                    UpdateMark24hrHPCC();
                }

                if (_sExecFlag == "6")
                {
                    UpdateMark24hrHPClear();
                }

                if (_sExecFlag == "A")
                {
                    UpdateMark24hrHPD();
                }

                if (_sExecFlag == "B")
                {
                    UpdateMark24hrHPE();
                }

                if (_sExecFlag == "C")
                {
                    UpdateIceiAssayDl6MstReset();
                }

                Console.WriteLine("========== icei2029b03 end ========== ");

                _proList.exitCode = 0;
                _proList.message = $"{AppDomain.CurrentDomain.FriendlyName} 執行完成";
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
                // PXX_exit_process(rtn_code, 0, msg);
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        /* ---------- parameter check ---------- */
        private static void CheckArg(string[] args)
        {
            _sExecFlag = args[0];
            Console.WriteLine($" s_exec_flag:[{_sExecFlag}] argc:[{args.Length}]");

            if (args.Length >= 2)
            {
                _sInputFeeYm = args[1];
                Console.WriteLine($"    s_input_fee_ym:[{_sInputFeeYm}]");
            }

            if (args.Length >= 3)
            {
                _sInputHospId = args[2];
                Console.WriteLine($"    s_input_hosp_id:[{_sInputHospId}]");
            }

            if (args.Length >= 4)
            {
                _sValidSDate = args[3];
                Console.WriteLine($"    s_valid_s_date:[{_sValidSDate}]");
            }

            if (args.Length >= 5)
            {
                _sMark24hr = args[4];
                Console.WriteLine($"    s_mark_24hr:[{_sMark24hr}]");
            }

            if (args.Length >= 6)
            {
                _wkBranchCode = args[5];
                Console.WriteLine($"    wk_branch_code:[{_wkBranchCode}]");
            }

            if (((_sExecFlag == "0" || _sExecFlag == "1") && (args.Length <= 1 || args.Length >= 4)))
            {
                _proList.exitCode = 1;
                ShowUsage();
                throw new ArgumentException();
            }

            if (_sExecFlag == "2" && (args.Length != 5 && args.Length != 6))
            {
                _proList.exitCode = 2;
                ShowUsage();
                throw new ArgumentException();
            }

            if (!(_sExecFlag == "0" || _sExecFlag == "1" || _sExecFlag == "2" ||
                  _sExecFlag == "3" || _sExecFlag == "4" || _sExecFlag == "5" ||
                  _sExecFlag == "6" || _sExecFlag == "7" || _sExecFlag == "8" ||
                  _sExecFlag == "9" || _sExecFlag == "A" || _sExecFlag == "B" ||
                  _sExecFlag == "C"))
            {
                _proList.exitCode = 3;
                ShowUsage();
                throw new ArgumentException();
            }

            if (args.Length == 6 &&
                !(_wkBranchCode == "1" || _wkBranchCode == "2" || _wkBranchCode == "3" ||
                  _wkBranchCode == "4" || _wkBranchCode == "5" || _wkBranchCode == "6"))
            {
                _proList.exitCode = 4;
                ShowUsage();
                throw new ArgumentException();
            }
        }

        private static void ShowUsage()
        {
            var prog = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine("執行類別： 0 - update MARK_24HR = 8 ( 24小時上傳註記: 8-例外院所 ) \n" +
                "           1 - update mark_560      ( Y : 醫令代碼 為 重要檢驗（查）結果之項目 ) \n" +
                " \n" +
                "參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)    \n" +
                " \n" +
                "範例1   ： icei2029b03  0  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 \n" +
                "範例2   ： icei2029b03  0  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 \n" +
                "範例3   ： icei2029b03  0  20190101  [all]        *參數給all，全部院所重新執行。  \n" +
                " \n" +
                "參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)    \n" +
                " \n" +
                "範例4   ： icei2029b03  1  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 \n" +
                "範例5   ： icei2029b03  1  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 \n" +
                "範例6   ： icei2029b03  1  20190101  [all]        *參數給all，全部院所重新執行。  \n" +
                "參數種類： 程式代號  執行類別  費用年月  醫事機構代碼(選項)  有效迄日 更新註記  分區別(選項)  \n" +
                " \n" +
                "範例7   ： icei2029b03  2  20190101  \"\"       20190101  A     *未給醫事機構代碼，只做尚未計算給付上限的院所。 \n" +
                "範例8   ： icei2029b03  2  20190101  [hosp_id]  20190101  A     *提供醫事機構代碼，只做該院所。 \n" +
                "範例9   ： icei2029b03  2  20190101  [all]      20190101  A  2  *參數給all，全部院所重新執行。  \n" +
                " \n" +
                "範例1   ： icei2029b03  3  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 \n" +
                "範例2   ： icei2029b03  3  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 \n" +
                "範例3   ： icei2029b03  3  20190101  [all]        *參數給all，全部院所重新執行。  \n" +
                " \n" +
                "範例1   ： icei2029b03  4  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 \n" +
                "範例2   ： icei2029b03  4  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 \n" +
                "範例3   ： icei2029b03  4  20190101  [all]        *參數給all，全部院所重新執行。  \n" +
                " \n" +
                "範例1   ： icei2029b03  5  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 \n" +
                "範例2   ： icei2029b03  5  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 \n" +
                "範例3   ： icei2029b03  5  20190101  [all]        *參數給all，全部院所重新執行。  \n" +
                " \n" +
                "範例1   ： icei2029b03  6  20190101  \"\"         *未給醫事機構代碼，只做尚未計算給付上限的院所。 \n" +
                "範例2   ： icei2029b03  6  20190101  [hosp_id]    *提供醫事機構代碼，只做該院所。 \n" +
                "範例3   ： icei2029b03  6  20190101  [all]        *參數給all，全部院所重新執行。  \n");
        }

        #region Update Methods
        private static void UpdateMark24Hr()
        {
            int iUpdateCount = 0;
            _iExeType = 1;

            Console.WriteLine($"    0 - update_mark_24_hr() - select pxxt_code s_exec_flag:[{_sExecFlag}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}]");

            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT code hosp_id,");
                strSQL.AppendLine("       to_char(VALID_S_DATE,'YYYYMMDD') recv_s_date,");
                strSQL.AppendLine("       to_char(VALID_E_DATE,'YYYYMMDD') recv_e_date,");
                strSQL.AppendLine("       to_char(VALID_S_DATE,'YYYYMM')||'01' fee_s_date,");
                strSQL.AppendLine("       to_char(VALID_E_DATE,'YYYYMM')||'01' fee_e_date");
                strSQL.AppendLine("  FROM pxxt_code");
                strSQL.AppendLine(" WHERE sub_sys   = 'ICE'");
                strSQL.AppendLine("   AND data_type = '109'");
                strSQL.AppendLine("   AND trunc(to_date(:sInputFeeYm,'YYYYMMDD'),'MM') between trunc(valid_s_date,'MM') and trunc(valid_e_date,'MM')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("   AND ( code      = :sInputHospId or");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("         'ALL'     = upper(:sInputHospIdUpper) or");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("         ( nvl(:sInputHospIdNull,0) = 0 and");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("           exe_time >= ( SELECT EXE_TIME");
                strSQL.AppendLine("                           FROM pxxt_code");
                strSQL.AppendLine("                          WHERE sub_sys   = 'ICE'");
                strSQL.AppendLine("                            AND data_type = '110'");
                strSQL.AppendLine("                            AND code = '1' ) ) )");

                cmd.CommandText = strSQL.ToString();
                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SQL100 sql100 = new SQL100
                        {
                            hospId = reader["hosp_id"].ToString(),
                            recvSDate = reader["recv_s_date"].ToString(),
                            recvEDate = reader["recv_e_date"].ToString(),
                            feeSDate = reader["fee_s_date"].ToString(),
                            feeEDate = reader["fee_e_date"].ToString()
                        };

                        StringBuilder updateSQL = new StringBuilder();
                        using (OracleCommand updateCmd = _oraConn.CreateCommand())
                        {
                            updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL a");
                            updateSQL.AppendLine("   SET MARK_24HR   = '8'");
                            updateSQL.AppendLine(" WHERE ( a.hosp_id = :hospId or :hospIdZero = '0000000000' )");
                            updateCmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                            updateCmd.Parameters.Add(new OracleParameter("hospIdZero", sql100.hospId));
                            updateSQL.AppendLine("   AND a.fee_ym     between to_date(:feeSDate ,'YYYYMMDD') and to_date(:feeEDate,'YYYYMMDD')");
                            updateCmd.Parameters.Add(new OracleParameter("feeSDate", sql100.feeSDate));
                            updateCmd.Parameters.Add(new OracleParameter("feeEDate", sql100.feeEDate));
                            updateSQL.AppendLine("   AND a.CASE_TIME  between to_date(:recvSDate,'YYYYMMDD') and to_date(:recvEDate,'YYYYMMDD')");
                            updateCmd.Parameters.Add(new OracleParameter("recvSDate", sql100.recvSDate));
                            updateCmd.Parameters.Add(new OracleParameter("recvEDate", sql100.recvEDate));
                            updateSQL.AppendLine("   AND a.MARK_24HR = '5'");
                            updateSQL.AppendLine("   AND a.recv_D_NUM > 0");

                            updateCmd.CommandText = updateSQL.ToString();
                            int rowsAffected = updateCmd.ExecuteNonQuery();

                            string msg = $"    update hosp_id:[{sql100.hospId}] fee_s_date:[{sql100.feeSDate}] fee_e_date:[{sql100.feeEDate}] recv_s_date:[{sql100.recvSDate}] recv_e_date:[{sql100.recvEDate}] sqlcode:[0] rec:[{rowsAffected}]";
                            Console.WriteLine(msg);
                            _logger.Info(msg);

                            iUpdateCount++;
                        }
                    }
                }
            }

            string updateMsg = $"    update pxxt_code s_input_hosp_id:[{_sInputHospId}] i_update_count:[{iUpdateCount}]";
            Console.WriteLine(updateMsg);
            _logger.Info(updateMsg);

            if (iUpdateCount > 0 &&
                (string.IsNullOrEmpty(_sInputHospId) ||
                 _sInputHospId.ToUpper() == "ALL"))
            {
                StringBuilder updatePxxtSQL = new StringBuilder();
                using (OracleCommand updatePxxtCmd = _oraConn.CreateCommand())
                {
                    updatePxxtSQL.AppendLine("UPDATE pxxt_code");
                    updatePxxtSQL.AppendLine("   SET EXE_TIME  = sysdate");
                    updatePxxtSQL.AppendLine(" WHERE sub_sys   = 'ICE'");
                    updatePxxtSQL.AppendLine("   AND data_type = '110'");
                    updatePxxtSQL.AppendLine("   AND code      = '1'");

                    updatePxxtCmd.CommandText = updatePxxtSQL.ToString();
                    int rowsAffected = updatePxxtCmd.ExecuteNonQuery();

                    string msg = $"update pxxt_code ICE data_type='110' code='1' sqlcode:[0] rec:[{rowsAffected}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);
                }
            }
        }

        private static void UpdateMark560()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId or");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) or");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_560()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET mark_560='Y'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND mark_560 is null");
                        updateSQL.AppendLine("   AND order_code in (");
                        updateSQL.AppendLine("       SELECT code FROM pxxt_code");
                        updateSQL.AppendLine("        WHERE sub_sys = 'PBA' AND data_type = '560'");
                        updateSQL.AppendLine("        UNION");
                        updateSQL.AppendLine("       SELECT '64164B' code FROM dual UNION");
                        updateSQL.AppendLine("       SELECT '64169B' code FROM dual UNION");
                        updateSQL.AppendLine("       SELECT '64202B' code FROM dual UNION");
                        updateSQL.AppendLine("       SELECT '64170B' code FROM dual UNION");
                        updateSQL.AppendLine("       SELECT '64162B' code FROM dual UNION");
                        updateSQL.AppendLine("       SELECT '64258B' code FROM dual UNION");
                        updateSQL.AppendLine("       SELECT '64201B' code FROM dual  )");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrPBA560()
        {
            int iLoopCount = 0;
            _iExeType = 1;

            Console.WriteLine($"    0 - update_mark_24hr_PBA_560() - select pxxt_code s_exec_flag:[{_sExecFlag}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}]");

            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT code order_code");
                strSQL.AppendLine("  FROM pxxt_code");
                strSQL.AppendLine(" WHERE sub_sys    = 'PBA'");
                strSQL.AppendLine("   AND data_type  = '560'");
                strSQL.AppendLine("   AND valid_s_date  = to_date(:sValidSDate,'yyyymmdd')");
                cmd.Parameters.Add(new OracleParameter("sValidSDate", _sValidSDate));

                cmd.CommandText = strSQL.ToString();
                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string wkOrderCode = reader["order_code"].ToString();

                        string msg = $"    update s_mark_24hr:[{_sMark24hr}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}] wk_order_code:[{wkOrderCode}] wk_branch_code:[{_wkBranchCode}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);

                        StringBuilder updateSQL = new StringBuilder();
                        using (OracleCommand updateCmd = _oraConn.CreateCommand())
                        {
                            updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL a");
                            updateSQL.AppendLine("   SET MARK_24HR     = :sMark24hr");
                            updateCmd.Parameters.Add(new OracleParameter("sMark24hr", _sMark24hr));
                            updateSQL.AppendLine(" WHERE ( a.hosp_id   = :sInputHospId OR upper(:sInputHospIdUpper) = 'ALL' OR NVL(:sInputHospIdNull,0)=0 )");
                            updateCmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                            updateCmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                            updateCmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                            updateSQL.AppendLine("   AND a.fee_ym      = to_date(:sInputFeeYm,'YYYYMMDD')");
                            updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                            updateSQL.AppendLine("   AND a.order_code  = :wkOrderCode");
                            updateCmd.Parameters.Add(new OracleParameter("wkOrderCode", wkOrderCode));
                            updateSQL.AppendLine("   AND a.BRANCH_CODE = ( CASE WHEN :wkBranchCode BETWEEN '1' AND '6' THEN :wkBranchCode ELSE a.BRANCH_CODE END )");
                            updateCmd.Parameters.Add(new OracleParameter("wkBranchCode", _wkBranchCode));
                            updateSQL.AppendLine("   AND a.MARK_24HR   = '5'");
                            updateSQL.AppendLine("   AND TO_DATE(substr(a.INIT_WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD') < last_day(add_months(A.fee_ym,1))+1");

                            updateCmd.CommandText = updateSQL.ToString();
                            int rowsAffected = updateCmd.ExecuteNonQuery();

                            msg = $"    sqlcode:[0] rec:[{rowsAffected}]";
                            Console.WriteLine(msg);
                            _logger.Info(msg);

                            iLoopCount++;
                        }
                    }
                }
            }

            string updateMsg = $"    update RAPI_ASSAY_DATA_FINAL i_loop_count:[{iLoopCount}]";
            Console.WriteLine(updateMsg);
            _logger.Info(updateMsg);
        }

        private static void UpdateMark24hrHPA()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='A'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        updateSQL.AppendLine("   AND MARK_24HR='5'");
                        updateSQL.AppendLine("   AND hosp_data_type IN ('22','21','29')");
                        updateSQL.AppendLine("   AND (txt_mark IS NULL OR txt_mark='3')");
                        updateSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                        updateSQL.AppendLine("   AND trunc(case_time) > trunc(nvl(out_date,APPL_E_DATE))");
                        updateSQL.AppendLine("   AND ( TO_DATE(substr(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')- trunc(case_time) ) <=2");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrHPAA()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='A'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        updateSQL.AppendLine("   AND MARK_24HR='5'");
                        updateSQL.AppendLine("   AND hosp_data_type IN ('22','21','29')");
                        updateSQL.AppendLine("   AND (txt_mark IS NULL OR txt_mark='3')");
                        updateSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                        updateSQL.AppendLine("   AND trunc(REAL_RECV_DATE) > trunc(nvl(out_date,APPL_E_DATE))");
                        updateSQL.AppendLine("   AND TO_DATE(substr(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-trunc(REAL_RECV_DATE) <=2");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrHPB()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='B'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        updateSQL.AppendLine("   AND MARK_24HR='5'");
                        updateSQL.AppendLine("   AND hosp_data_type IN ('22','21','29')");
                        updateSQL.AppendLine("   AND (txt_mark IS NULL OR txt_mark='3')");
                        updateSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                        updateSQL.AppendLine("   AND trunc(case_time) < trunc(in_date)");
                        updateSQL.AppendLine("   AND (TO_DATE(substr(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-trunc(nvl(out_date,APPL_E_DATE)))  <=2");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrHPBB()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='B'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        updateSQL.AppendLine("   AND MARK_24HR='5'");
                        updateSQL.AppendLine("   AND hosp_data_type IN ('22','21','29')");
                        updateSQL.AppendLine("   AND (txt_mark IS NULL OR txt_mark='3')");
                        updateSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                        updateSQL.AppendLine("   AND trunc(REAL_RECV_DATE) < trunc(in_date)");
                        updateSQL.AppendLine("   AND TO_DATE(substr(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-trunc(nvl(out_date,APPL_E_DATE))  <=2");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrHPC()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='C'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        updateSQL.AppendLine("   AND MARK_24HR='5'");
                        updateSQL.AppendLine("   AND hosp_data_type IN ('22','21','29')");
                        updateSQL.AppendLine("   AND (txt_mark IS NULL OR txt_mark='3')");
                        updateSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                        updateSQL.AppendLine("   AND trunc(case_time) BETWEEN trunc(in_date) AND trunc(nvl(out_date,APPL_E_DATE))");
                        updateSQL.AppendLine("   AND TO_DATE(substr(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-trunc(nvl(out_date,APPL_E_DATE))  <=2");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrHPCC()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='C'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        updateSQL.AppendLine("   AND MARK_24HR='5'");
                        updateSQL.AppendLine("   AND hosp_data_type IN ('22','21','29')");
                        updateSQL.AppendLine("   AND (txt_mark IS NULL OR txt_mark='3')");
                        updateSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                        updateSQL.AppendLine("   AND trunc(REAL_RECV_DATE) BETWEEN trunc(in_date) AND trunc(nvl(out_date,APPL_E_DATE))");
                        updateSQL.AppendLine("   AND TO_DATE(substr(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-trunc(nvl(out_date,APPL_E_DATE))  <=2");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrHPD()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='D'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        updateSQL.AppendLine("   AND MARK_24HR='5'");
                        updateSQL.AppendLine("   AND hosp_data_type IN ('22','21','29')");
                        updateSQL.AppendLine("   AND (txt_mark IS NULL OR txt_mark='3')");
                        updateSQL.AppendLine("   AND (RECARD_MARK='2' AND trunc(TREAT_DT) > trunc(case_time))");
                        updateSQL.AppendLine("   AND trunc(TREAT_DT) > trunc(nvl(out_date,APPL_E_DATE))");
                        updateSQL.AppendLine("   AND TO_DATE(substr(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-trunc(TREAT_DT) <=2");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrHPE()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='E'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                        updateSQL.AppendLine("   AND MARK_24HR='5'");
                        updateSQL.AppendLine("   AND hosp_data_type IN ('22','21','29')");
                        updateSQL.AppendLine("   AND (txt_mark IS NULL OR txt_mark='3')");
                        updateSQL.AppendLine("   AND (RECARD_MARK='2' AND trunc(TREAT_DT) > trunc(case_time))");
                        updateSQL.AppendLine("   AND trunc(TREAT_DT) BETWEEN trunc(in_date) AND trunc(nvl(out_date,APPL_E_DATE))");
                        updateSQL.AppendLine("   AND ( TO_DATE(substr(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')- trunc(nvl(out_date,APPL_E_DATE)))  <=2");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateMark24hrHPClear()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id AND b.MARK_24HR IN ('A','B','C','D','E')");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                        updateSQL.AppendLine("   SET MARK_24HR='5'");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND MARK_24HR IN ('A','B','C','D','E')");

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }

        private static void UpdateIceiAssayDl6MstReset()
        {
            if (_oraConn.State != ConnectionState.Open)
                _oraConn.Open();

            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT hosp_id");
                strSQL.AppendLine("  FROM mhat_hospbsc a");
                strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id AND b.MARK_24HR IN ('A','B','C','D','E')");
                strSQL.AppendLine("                   AND b.fee_ym  = to_date(:sInputFeeYm,'YYYYMMDD')");
                cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                strSQL.AppendLine("                         'ALL'   = upper(:sInputHospIdUpper) OR");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId.ToUpper()));
                strSQL.AppendLine("                         nvl(:sInputHospIdNull,0) = 0 )");
                cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? (object)DBNull.Value : _sInputHospId));
                strSQL.AppendLine("               )");
                strSQL.AppendLine(" ORDER BY hosp_id ASC");

                cmd.CommandText = strSQL.ToString();
                string[] hospIds = new string[80000];
                int iHospIdCount = 0;

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read() && iHospIdCount < 80000)
                    {
                        hospIds[iHospIdCount++] = reader["hosp_id"].ToString();
                    }
                }

                Console.WriteLine($"    1 - update_mark_24hr_HP_A()-  select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");
                Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}]");

                for (int iLoop = 0; iLoop < iHospIdCount; iLoop++)
                {
                    string msg = $"    update hosp_id[{iLoop}]:[{hospIds[iLoop]}]";
                    Console.WriteLine(msg);
                    _logger.Info(msg);

                    StringBuilder updateSQL = new StringBuilder();
                    using (OracleCommand updateCmd = _oraConn.CreateCommand())
                    {
                        updateSQL.AppendLine("UPDATE icei_assay_dl6_mst SET assay_status=null");
                        updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                        updateCmd.Parameters.Add(new OracleParameter("hospId", hospIds[iLoop]));
                        updateSQL.AppendLine("   AND fee_ym  >= to_date(:sInputFeeYm,'YYYYMMDD')");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                        updateSQL.AppendLine("   AND fee_ym  <= add_months(to_date(:sInputFeeYmAddMonths,'YYYYMMDD'),2)");
                        updateCmd.Parameters.Add(new OracleParameter("sInputFeeYmAddMonths", _sInputFeeYm));

                        updateCmd.CommandText = updateSQL.ToString();
                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        msg = $" sqlcode:[0] rec:[{rowsAffected}]";
                        Console.WriteLine(msg);
                        _logger.Info(msg);
                    }
                }
            }
        }
        #endregion
    }
}
```