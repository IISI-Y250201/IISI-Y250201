```csharp
/*******************************************************************
程式代碼：icei2018b01
程式名稱：人工關節明細收載作業(月)
功能簡述：處理人工關節明細收載作業
參    數：
參數一：分區別(必填)
參數二：費用年月(YYYYMMDD)(選項)
參數三：醫事機構代碼(選項)
範例一：icei2018b01 3 20180101 1137010024
讀取檔案：無
異動檔案：無
作    者：系統轉換
歷次修改時間：
1.2023/01/01
需求單號暨修改內容簡述：
1.Pro*C轉C#
備    註：
********************************************************************/

using System;
using System.Data;
using System.Text;
using System.IO;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei2018b01
{
    public class icei2018b01
    {
        #region Static Members
        private static OracleConnection _oraConn = new OracleConnection(GetDBInfo.GetHmDBConnectString);
        private static Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());
        private static ProList _proList = new ProList();
        #endregion

        #region Constants
        private const int PB0_FOUND = 0;
        private const int PB0_NOT_FOUND = 1;
        private const int CONTINUE = 0;
        private const int EXIT = 0;
        private const int SUCCESS = 0;
        private const int NOT_FOUND = 1403;
        private const int NOT_CONV_ID_IN_TEST_ENV = 1;
        private const int EXIT_WHEN_SQLERR_IN_TEST_ENV = 1;
        private const bool TRUE = true;
        private const bool FALSE = false;
        #endregion

        #region Variables
        private static string _inputFeeYm = string.Empty;
        private static string _inputHospId = string.Empty;
        private static string _weekHour = string.Empty;
        private static string _wkBranchCode = string.Empty;
        private static string _wkHospId = string.Empty;
        private static string _wkHospIdM = string.Empty;
        private static string _wkRealHospId = string.Empty;
        private static string _inputBranchCode = string.Empty;
        private static string _wkHospCntType = string.Empty;
        private static string _wkFeeYm = string.Empty;
        private static string _wkFeeQq = string.Empty;
        private static string _wkIndexType = string.Empty;
        private static string _wkViewMonth = string.Empty;
        private static string _tmpCurrTimes = string.Empty;
        private static int _wkSeasonCnt = 0;
        private static int _wkSeasonTot = 13;
        private static int _recCnt = 0;
        private static int _iWriteSqlCtrl = 0;
        private static int _iLoopCount = 0;
        private static string _wkRealBranchCode = string.Empty;
        private static string _wkRealHospCntType = string.Empty;
        private static string _wkConvIdc = string.Empty;
        private static int _iSqlcode = 0;
        private static int _iRec = 0;
        private static int _iExitWhenSqlerr = 1;
        private static int _iSqlRecCount = 0;
        private static bool _iCheck1700 = TRUE;
        private static string _sInAssayHosp = string.Empty;
        private static int _iMergeHosp = 0;
        #endregion

        #region Main Method
        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                _wkIndexType = "3";

                _inputBranchCode = "0";
                if (args.Length >= 1)
                {
                    _inputBranchCode = args[0];
                }

                _inputFeeYm = string.Empty;
                if (args.Length >= 2)
                {
                    _inputFeeYm = args[1];
                }

                _inputHospId = string.Empty;
                if (args.Length == 3)
                {
                    _inputHospId = args[2];
                }

                CheckArg(args);

                _wkConvIdc = "encrypt_aes(decrypt_aes";

                if (Environment.GetEnvironmentVariable("ORACLE_SID") == "NHIHMT")
                {
                    if (EXIT_WHEN_SQLERR_IN_TEST_ENV == 1)
                    {
                        _iExitWhenSqlerr = 1;
                    }

                    if (NOT_CONV_ID_IN_TEST_ENV == 1)
                    {
                        _wkConvIdc = "(";
                    }
                }

                WriteMsg($"\n ================================\n" +
                         $" input_branch_code  : [{_inputBranchCode}] \n" +
                         $" input_fee_ym       : [{_inputFeeYm}] \n" +
                         $" input_hosp_id      : [{_inputHospId}] \n" +
                         $"\n ================================\n");

                WriteMsg($"argc:[{args.Length}] \n");

                _iWriteSqlCtrl = 0;

                StringBuilder strSQL = new StringBuilder();
                StringBuilder strSQLSubquery = new StringBuilder();

                switch (args.Length)
                {
                    case 1: // 參數:分區別
                        strSQLSubquery.AppendLine("            SELECT DISTINCT hosp_id,  ");
                        strSQLSubquery.AppendLine("                   fee_ym             ");
                        strSQLSubquery.AppendLine("              FROM ICEI_HOSP_FLOW a   ");
                        strSQLSubquery.AppendLine($"             WHERE branch_code = '{_inputBranchCode}' ");
                        strSQLSubquery.AppendLine("               AND fee_ym     >= TO_DATE('20180101','YYYYMMDD')  ");
                        strSQLSubquery.AppendLine("               AND hosp_cnt_type < '4' ");
                        strSQLSubquery.AppendLine("               AND ( EXISTS           ");
                        strSQLSubquery.AppendLine("                     ( SELECT 1       ");
                        strSQLSubquery.AppendLine("                         FROM ICEI_ASSAY_DL3_MST b        ");
                        strSQLSubquery.AppendLine("                        WHERE b.fee_ym  >= TO_DATE('20180101','YYYYMMDD') ");
                        strSQLSubquery.AppendLine("                          AND b.fee_ym   = a.fee_ym       ");
                        strSQLSubquery.AppendLine("                          AND b.hosp_id  = a.hosp_id      ");
                        strSQLSubquery.AppendLine("                          AND b.proc_status IS NULL )     ");
                        strSQLSubquery.AppendLine("                     OR NOT EXISTS                        ");
                        strSQLSubquery.AppendLine("                     ( SELECT 1                           ");
                        strSQLSubquery.AppendLine("                         FROM ICEI_ASSAY_DL3_MST c        ");
                        strSQLSubquery.AppendLine("                        WHERE c.fee_ym  >= TO_DATE('20180101','YYYYMMDD') ");
                        strSQLSubquery.AppendLine("                          AND c.fee_ym   = a.fee_ym       ");
                        strSQLSubquery.AppendLine("                          AND c.hosp_id  = a.hosp_id  ))  ");

                        WriteMsg($"case 1:[\n{strSQLSubquery}\n]");
                        break;

                    case 2: // 參數:分區別，費用年月
                        strSQLSubquery.AppendLine("           SELECT DISTINCT hosp_id,  ");
                        strSQLSubquery.AppendLine("                  fee_ym             ");
                        strSQLSubquery.AppendLine("             FROM ICEI_HOSP_FLOW a   ");
                        strSQLSubquery.AppendLine($"            WHERE branch_code = '{_inputBranchCode}' ");
                        strSQLSubquery.AppendLine($"              AND fee_ym      = TO_DATE('{_inputFeeYm}','YYYYMMDD') ");
                        strSQLSubquery.AppendLine("              AND hosp_cnt_type < '4' ");
                        strSQLSubquery.AppendLine("              AND ( EXISTS           ");
                        strSQLSubquery.AppendLine("                     ( SELECT 1      ");
                        strSQLSubquery.AppendLine("                         FROM ICEI_ASSAY_DL3_MST b        ");
                        strSQLSubquery.AppendLine("                        WHERE b.fee_ym  >= TO_DATE('20180101','YYYYMMDD') ");
                        strSQLSubquery.AppendLine("                          AND b.fee_ym   = a.fee_ym       ");
                        strSQLSubquery.AppendLine("                          AND b.hosp_id  = a.hosp_id      ");
                        strSQLSubquery.AppendLine("                          AND b.proc_status IS NULL )     ");
                        strSQLSubquery.AppendLine("                     OR NOT EXISTS                        ");
                        strSQLSubquery.AppendLine("                     ( SELECT 1                           ");
                        strSQLSubquery.AppendLine("                         FROM ICEI_ASSAY_DL3_MST c        ");
                        strSQLSubquery.AppendLine("                        WHERE c.fee_ym  >= TO_DATE('20180101','YYYYMMDD') ");
                        strSQLSubquery.AppendLine("                          AND c.fee_ym   = a.fee_ym       ");
                        strSQLSubquery.AppendLine("                          AND c.hosp_id  = a.hosp_id  ))  ");

                        WriteMsg($"case 2:[\n{strSQLSubquery}\n]");
                        break;

                    case 3:
                        _iCheck1700 = FALSE;
                        WriteMsg($"case 3 set i_check_1700:[{_iCheck1700}]");

                        if (_inputHospId == "ALL")
                        {
                            using (OracleCommand cmd = _oraConn.CreateCommand())
                            {
                                _oraConn.Open();
                                cmd.CommandText = "UPDATE ICEI_ASSAY_DL3_MST SET proc_status = NULL " +
                                                 $"WHERE branch_code = :inputBranchCode " +
                                                 $"AND fee_ym = TO_DATE(:inputFeeYm,'YYYYMMDD') " +
                                                 $"AND proc_status = 'Y'";
                                cmd.Parameters.Add(new OracleParameter("inputBranchCode", _inputBranchCode));
                                cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));

                                _iSqlcode = 0;
                                try
                                {
                                    int rowsAffected = cmd.ExecuteNonQuery();
                                    WriteMsg($"UPDATE proc_status = NULL branch_code:[{_inputBranchCode}] fee_ym:[{_inputFeeYm}] sqlcode:[0] rec:[{rowsAffected}] \n");
                                }
                                catch (OracleException ex)
                                {
                                    _iSqlcode = ex.ErrorCode;
                                    if (_iSqlcode != 0 && _iSqlcode != 1403)
                                    {
                                        WriteMsg($"UPDATE ICEI_ASSAY_DL3_MST = NULL error! input_branch_code:[{_inputBranchCode}] input_fee_ym[{_inputFeeYm}] sqlcode:[{_iSqlcode}] rec:[0]\n");
                                        _proList.exitCode = 50;
                                        _proList.message = "UPDATE ICEI_ASSAY_DL3_MST error";
                                        throw;
                                    }
                                }

                                cmd.CommandText = "COMMIT";
                                cmd.ExecuteNonQuery();
                            }

                            strSQLSubquery.AppendLine("           SELECT DISTINCT hosp_id,  ");
                            strSQLSubquery.AppendLine("                  fee_ym             ");
                            strSQLSubquery.AppendLine("             FROM ICEI_HOSP_FLOW a   ");
                            strSQLSubquery.AppendLine($"            WHERE branch_code = '{_inputBranchCode}' ");
                            strSQLSubquery.AppendLine($"              AND fee_ym      = TO_DATE('{_inputFeeYm}','YYYYMMDD') ");
                            strSQLSubquery.AppendLine("              AND hosp_cnt_type < '4' ");
                            strSQLSubquery.AppendLine("              AND ( EXISTS           ");
                            strSQLSubquery.AppendLine("                     ( SELECT 1       ");
                            strSQLSubquery.AppendLine("                         FROM ICEI_ASSAY_DL3_MST b        ");
                            strSQLSubquery.AppendLine("                        WHERE b.fee_ym  >= TO_DATE('20180101','YYYYMMDD') ");
                            strSQLSubquery.AppendLine("                          AND b.fee_ym   = a.fee_ym       ");
                            strSQLSubquery.AppendLine("                          AND b.hosp_id  = a.hosp_id      ");
                            strSQLSubquery.AppendLine("                          AND b.proc_status IS NULL )     ");
                            strSQLSubquery.AppendLine("                     OR NOT EXISTS                        ");
                            strSQLSubquery.AppendLine("                     ( SELECT 1                           ");
                            strSQLSubquery.AppendLine("                         FROM ICEI_ASSAY_DL3_MST c        ");
                            strSQLSubquery.AppendLine("                        WHERE c.fee_ym  >= TO_DATE('20180101','YYYYMMDD') ");
                            strSQLSubquery.AppendLine("                          AND c.fee_ym   = a.fee_ym       ");
                            strSQLSubquery.AppendLine("                          AND c.hosp_id  = a.hosp_id  ))  ");

                            WriteMsg($"case 3 ALL:[\n{strSQLSubquery}\n]");
                        }
                        else
                        {
                            _iWriteSqlCtrl = 2;

                            strSQLSubquery.AppendLine("           SELECT DISTINCT hosp_id,  ");
                            strSQLSubquery.AppendLine("                  fee_ym             ");
                            strSQLSubquery.AppendLine("             FROM ICEI_HOSP_FLOW a   ");
                            strSQLSubquery.AppendLine($"            WHERE branch_code = '{_inputBranchCode}' ");
                            strSQLSubquery.AppendLine($"              AND fee_ym      = TO_DATE('{_inputFeeYm}','YYYYMMDD')  ");
                            strSQLSubquery.AppendLine("              AND hosp_cnt_type < '4' ");
                            strSQLSubquery.AppendLine($"              AND hosp_id     = '{_inputHospId}' ");

                            WriteMsg($"case 3 else:[\n{strSQLSubquery}\n]");
                        }
                        break;

                    default:
                        break;
                }

                WriteMsg("=========================");

                if (_inputHospId == "ALL")
                {
                    strSQL.AppendLine("\n SELECT B.branch_code,    ");
                    strSQL.AppendLine("          A.hosp_id,        ");
                    strSQL.AppendLine("          B.hosp_cnt_type,  ");
                    strSQL.AppendLine("          (TO_CHAR(A.fee_ym,'yyyy') - '2014') * 4 + TO_CHAR(A.fee_ym,'Q') season_tot,  ");
                    strSQL.AppendLine("          TO_CHAR(A.fee_ym,'yyyymmdd') fee_ym,  ");
                    strSQL.AppendLine("          TO_CHAR(A.fee_ym,'Q') fee_qq,         ");
                    strSQL.AppendLine("          TO_NUMBER(TO_CHAR(A.fee_ym,'Q')) season_cnt,  ");
                    strSQL.AppendLine("          '3' index_type,   ");
                    strSQL.AppendLine("          TO_CHAR(TO_CHAR(A.fee_ym,'yyyy')-1911)||TO_CHAR(A.fee_ym,'mm') view_ym  ");
                    strSQL.AppendLine("     FROM ( ");
                    strSQL.AppendLine(strSQLSubquery.ToString());
                    strSQL.AppendLine("           ) A, MHAT_HOSPBSC b,            ");
                    strSQL.AppendLine("          (  SELECT hosp_id,fee_ym,MIN(TO_CHAR(txt_date,'YYYYMMDD')) txt_date   ");
                    strSQL.AppendLine("               FROM ICEI_ASSAY_DL3_MST     ");
                    strSQL.AppendLine($"              WHERE fee_ym = TO_DATE('{_inputFeeYm}','YYYYMMDD')  ");
                    strSQL.AppendLine("           GROUP BY hosp_id,fee_ym ) c     ");
                    strSQL.AppendLine("     WHERE A.hosp_id = B.hosp_id                ");
                    strSQL.AppendLine("       AND A.hosp_id = C.hosp_id (+)   ");
                    strSQL.AppendLine("       AND A.fee_ym  = C.fee_ym  (+)   ");
                    strSQL.AppendLine("  ORDER BY C.txt_date ASC,A.fee_ym, SUBSTR(A.hosp_id,10,1), SUBSTR(A.hosp_id,5,2), A.hosp_id  ");
                }
                else
                {
                    strSQL.AppendLine("\n SELECT B.branch_code,    ");
                    strSQL.AppendLine("          A.hosp_id,        ");
                    strSQL.AppendLine("          B.hosp_cnt_type,  ");
                    strSQL.AppendLine("          (TO_CHAR(fee_ym,'yyyy') - '2014') * 4 + TO_CHAR(fee_ym,'Q') season_tot,  ");
                    strSQL.AppendLine("          TO_CHAR(fee_ym,'yyyymmdd') fee_ym,  ");
                    strSQL.AppendLine("          TO_CHAR(fee_ym,'Q') fee_qq,         ");
                    strSQL.AppendLine("          TO_NUMBER(TO_CHAR(fee_ym,'Q')) season_cnt,  ");
                    strSQL.AppendLine("          '3' index_type,   ");
                    strSQL.AppendLine("          TO_CHAR(TO_CHAR(fee_ym,'yyyy')-1911)||TO_CHAR(fee_ym,'mm') view_ym  ");
                    strSQL.AppendLine("     FROM ( ");
                    strSQL.AppendLine(strSQLSubquery.ToString());
                    strSQL.AppendLine("           ) A, MHAT_HOSPBSC b                  ");
                    strSQL.AppendLine("     WHERE A.hosp_id = B.hosp_id                ");
                    strSQL.AppendLine("  ORDER BY fee_ym, SUBSTR(A.hosp_id,10,1), SUBSTR(A.hosp_id,5,2), A.hosp_id  ");
                }

                WriteMsg($"strSQL:[\n{strSQL}\n]");
                WriteMsg("=========================");

                if(_oraConn.)

                _oraConn.Open();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = strSQL.ToString();
                    WriteDate();

                    try
                    {
                        using (OracleDataReader reader = cmd.ExecuteReader())
                        {
                            int status2 = 0;

                            while (status2 == 0)
                            {
                                _iLoopCount++;

                                if (_iLoopCount <= 10)
                                {
                                    _iWriteSqlCtrl = 2;
                                }
                                else
                                {
                                    _iWriteSqlCtrl = 0;
                                }

                                WriteMsg($"wk_hosp_id[{_wkHospId}] i_loop_count:[{_iLoopCount}] i_write_sql_ctrl:[{_iWriteSqlCtrl}] \n");

                                _wkBranchCode = string.Empty;
                                _wkHospId = string.Empty;
                                _wkHospCntType = string.Empty;
                                _wkFeeYm = string.Empty;
                                _wkFeeQq = string.Empty;
                                _wkSeasonCnt = 0;
                                _wkIndexType = string.Empty;
                                _iMergeHosp = 0;
                                _wkViewMonth = string.Empty;
                                _sInAssayHosp = string.Empty;

                                if (!reader.Read())
                                {
                                    break;
                                }

                                _wkBranchCode = reader.GetString(0);
                                _wkHospId = reader.GetString(1);
                                _wkHospCntType = reader.GetString(2);
                                _wkSeasonTot = reader.GetInt32(3);
                                _wkFeeYm = reader.GetString(4);
                                _wkFeeQq = reader.GetString(5);
                                _wkSeasonCnt = reader.GetInt32(6);
                                _wkIndexType = reader.GetString(7);
                                _wkViewMonth = reader.GetString(8);

                                using (OracleCommand updateCmd = _oraConn.CreateCommand())
                                {
                                    updateCmd.CommandText = "UPDATE ICEI_ASSAY_DL3_MST SET proc_status = '1' " +
                                                          $"WHERE branch_code = :wkBranchCode " +
                                                          $"AND hosp_id = :wkHospId " +
                                                          $"AND fee_ym = TO_DATE(:wkFeeYm,'YYYYMMDD')";
                                    updateCmd.Parameters.Add(new OracleParameter("wkBranchCode", _wkBranchCode));
                                    updateCmd.Parameters.Add(new OracleParameter("wkHospId", _wkHospId));
                                    updateCmd.Parameters.Add(new OracleParameter("wkFeeYm", _wkFeeYm));

                                    _iSqlcode = 0;
                                    try
                                    {
                                        updateCmd.ExecuteNonQuery();
                                    }
                                    catch (OracleException ex)
                                    {
                                        _iSqlcode = ex.ErrorCode;
                                        if (_iSqlcode != 0 && _iSqlcode != 1403)
                                        {
                                            WriteMsg($"UPDATE ICEI_ASSAY_DL3_MST error! wk_branch_code:[{_wkBranchCode}] wk_hosp_id[{_wkHospId}] wk_fee_ym[{_wkFeeYm}] sqlcode:[{_iSqlcode}] rec:[0]\n");
                                            _proList.exitCode = 60;
                                            _proList.message = "UPDATE ICEI_ASSAY_DL3_MST error";
                                            throw;
                                        }
                                    }

                                    updateCmd.CommandText = "COMMIT";
                                    updateCmd.ExecuteNonQuery();
                                }

                                _wkHospIdM = string.Empty;
                                _wkRealHospId = string.Empty;
                                _iMergeHosp = 0;

                                using (OracleCommand mergeCmd = _oraConn.CreateCommand())
                                {
                                    mergeCmd.CommandText = "SELECT code_cname, code " +
                                                         "FROM PXXT_CODE A " +
                                                         "WHERE sub_sys = 'PXX' " +
                                                         "AND data_type = '011' " +
                                                         "AND code = :wkHospId " +
                                                         "AND code != code_cname " +
                                                         "AND TRUNC(valid_s_date,'mm') <= TO_DATE(:wkFeeYm,'yyyymmdd') " +
                                                         "AND TRUNC(valid_e_date,'mm') >= TO_DATE(:wkFeeYm,'yyyymmdd') " +
                                                         "AND ROWNUM = 1";
                                    mergeCmd.Parameters.Add(new OracleParameter("wkHospId", _wkHospId));
                                    mergeCmd.Parameters.Add(new OracleParameter("wkFeeYm", _wkFeeYm));

                                    try
                                    {
                                        using (OracleDataReader mergeReader = mergeCmd.ExecuteReader())
                                        {
                                            if (mergeReader.Read())
                                            {
                                                _wkHospIdM = mergeReader.GetString(0);
                                                _wkRealHospId = mergeReader.GetString(1);

                                                if ((_wkFeeYm.CompareTo("20190101") >= 0 && _wkFeeYm.CompareTo("20190601") <= 0) ||
                                                    _wkRealHospId == "1131100010" || _wkRealHospId == "1101100020")
                                                {
                                                    _iMergeHosp = 1;
                                                    _sInAssayHosp = $" ('{_wkRealHospId}','{_wkHospIdM}') ";
                                                    WriteMsg($" i_merge_hosp:[{_iMergeHosp}] 1 合併申報院所，檢驗(查)資料主院所 合併院所 同時查詢。 wk_real_hosp_id:[{_wkRealHospId}] wk_hosp_id_m:[{_wkHospIdM}] \n");
                                                }
                                                else
                                                {
                                                    _iMergeHosp = 2;
                                                    _sInAssayHosp = $" ('{_wkRealHospId}') ";
                                                    WriteMsg($" i_merge_hosp:[{_iMergeHosp}] 2 由 real_hosp_id 上傳檢驗資料。 wk_real_hosp_id:[{_wkRealHospId}] wk_hosp_id_m:[{_wkHospIdM}] \n");
                                                }
                                            }
                                            else
                                            {
                                                _iMergeHosp = 0;
                                                _wkHospIdM = _wkHospId;
                                                _wkRealHospId = _wkHospId;
                                                _sInAssayHosp = $" ('{_wkRealHospId}') ";
                                                WriteMsg($" i_merge_hosp:[{_iMergeHosp}] 0 非合併申報院所。 wk_real_hosp_id:[{_wkRealHospId}] wk_hosp_id_m:[{_wkHospIdM}] \n");
                                            }
                                        }
                                    }
                                    catch (OracleException ex)
                                    {
                                        _iMergeHosp = 0;
                                        _wkHospIdM = _wkHospId;
                                        _wkRealHospId = _wkHospId;
                                        _sInAssayHosp = $" ('{_wkRealHospId}') ";
                                        WriteMsg($" i_merge_hosp:[{_iMergeHosp}] 0 非合併申報院所。 wk_real_hosp_id:[{_wkRealHospId}] wk_hosp_id_m:[{_wkHospIdM}] \n");
                                    }
                                }

                                WriteMsg($" hosp_id in {_sInAssayHosp}  <-- s_in_assay_hosp \n");

                                if (_iCheck1700 == TRUE && CheckPxxtHospFlowStatus1700() == PB0_NOT_FOUND)
                                {
                                    WriteMsg($" i_check_1700:[{_iCheck1700}] check \n");
                                    continue;
                                }
                                else
                                {
                                    WriteMsg($" i_check_1700:[{_iCheck1700}] pass \n");
                                }

                                using (OracleCommand hospCmd = _oraConn.CreateCommand())
                                {
                                    hospCmd.CommandText = "SELECT branch_code, hosp_cnt_type " +
                                                         "FROM MHAT_HOSPBSC " +
                                                         "WHERE hosp_id = :wkRealHospId";
                                    hospCmd.Parameters.Add(new OracleParameter("wkRealHospId", _wkRealHospId));

                                    using (OracleDataReader hospReader = hospCmd.ExecuteReader())
                                    {
                                        if (hospReader.Read())
                                        {
                                            _wkRealBranchCode = hospReader.GetString(0);
                                            _wkRealHospCntType = hospReader.GetString(1);
                                        }
                                    }
                                }

                                WriteMsg($"wk_hosp_id[{_wkHospId}] wk_fee_ym[{_wkFeeYm}] wk_real_hosp_id[{_wkRealHospId}] wk_hosp_id_m[{_wkHospIdM}] wk_real_hosp_cnt_type:[{_wkRealHospCntType}] wk_real_branch_code:[{_wkRealBranchCode}] ");

                                WriteDate();
                                WriteMsg("\n==========================================================\n");
                                Fun1DelHmDtl();
                                WriteMsg("\n==========================================================\n");
                                Fun1InitHmMst();
                                WriteMsg("\n==========================================================\n");
                                Fun1DelHmSts();
                                WriteMsg("\n==========================================================\n");
                                Fun2DelIdc();
                                WriteMsg("\n==========================================================\n");
                                Fun4HpInsHm();
                                WriteMsg("\n==========================================================\n");
                                Fun5AssayInsHm();
                                WriteMsg("\n==========================================================\n");
                                Fun6InsIdc();
                                WriteMsg("\n==========================================================\n");
                                Fun7InsHmMst();
                                WriteMsg("\n==========================================================\n");
                                Fun8InsHmSts();
                                WriteMsg("\n==========================================================\n");
                                Fun1DelHmDtl();
                                WriteMsg("\n==========================================================\n");
                                Fun9UpdateHmIceiAssayDtl();
                                WriteDate();

                                _recCnt++;
                            }
                        }
                    }
                    catch (OracleException ex)
                    {
                        string msg = $" open CURSOR_sql100 input_branch_code:[{_inputBranchCode}] input_fee_ym:[{_inputFeeYm}] error code={ex.ErrorCode}";
                        Console.WriteLine(msg);
                        _logger.Error(msg);
                        _proList.exitCode = 10;
                        _proList.message = msg;
                        throw;
                    }
                }

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
                // Original: PXX_exit_process();
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }
        #endregion

        #region Helper Methods
        private static void CheckArg(string[] args)
        {
            if (args.Length < 1 || args.Length > 3)
            {
                var prog = AppDomain.CurrentDomain.FriendlyName;
                string msg = $"\n錯誤: 參數個數不符 : argc=[{args.Length}] \n" +
                             $" Usage:{prog} 分區別(必填) 費用年月(YYYYMMDD)(選項) 醫事機構代碼(選項) \n" +
                             $" 參數一   : input_branch_code  : [{_inputBranchCode}] \n" +
                             $" 參數二   : input_fee_ym       : [{_inputFeeYm}] \n" +
                             $" 參數三   : input_hosp_id      : [{_inputHospId}] \n\n" +
                             $" 例一     : icei2018b01 3 20180101 1137010024 \n" +
                             $" 例二     : icei2018b01 3 20180101 1137010042 \n" +
                             $" 例三     : icei2018b01 3 20180101    \n";

                Console.WriteLine(msg);
                _logger.Error(msg);
                _proList.exitCode = 1;
                _proList.message = "錯誤 : 參數個數不符.\n\n";
                throw new ArgumentException(msg);
            }
        }

        private static void WriteMsg(string message)
        {
            Console.WriteLine($"\n {message}");
            _logger.Info(message);
        }

        private static void WriteDate()
        {
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYY/MM/DD HH24:MI:SS') FROM DUAL";
                string dateTime = cmd.ExecuteScalar().ToString();
                WriteMsg($"sysdate:[{dateTime}]");
            }
        }

        private static int ExecuteSQL(string sql)
        {
            string startDateTime = string.Empty;
            string endDateTime = string.Empty;
            int iWriteSql = 1;

            using (OracleCommand startCmd = _oraConn.CreateCommand())
            {
                startCmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYY/MM/DD HH24:MI:SS') FROM DUAL";
                startDateTime = startCmd.ExecuteScalar().ToString();
            }

            _iSqlcode = 0;
            _iSqlRecCount = 0;

            try
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    _iSqlRecCount = cmd.ExecuteNonQuery();
                }
            }
            catch (OracleException ex)
            {
                _iSqlcode = ex.ErrorCode;
                if (_iWriteSqlCtrl == 0)
                {
                    iWriteSql = 0;
                }
                else if (_iWriteSqlCtrl == 1 && _iSqlcode != 0 && _iSqlcode != 1403)
                {
                    iWriteSql = 1;
                }
                else if (_iWriteSqlCtrl == 2)
                {
                    iWriteSql = 1;
                }

                if (iWriteSql == 1)
                {
                    WriteMsg($"\nexecuteSQL :[{sql}]");
                }

                if (_iSqlcode != 0 && _iSqlcode != 1403 && _iExitWhenSqlerr == 1)
                {
                    _proList.exitCode = 70;
                    _proList.message = "SQL execution error";
                    throw;
                }
            }

            using (OracleCommand endCmd = _oraConn.CreateCommand())
            {
                endCmd.CommandText = "SELECT TO_CHAR(SYSDATE,'YYYY/MM/DD HH24:MI:SS') FROM DUAL";
                endDateTime = endCmd.ExecuteScalar().ToString();
            }

            WriteMsg($"Start     :[{startDateTime}]");
            WriteMsg($"End       :[{endDateTime}]");
            WriteMsg($"i_sqlcode :[{_iSqlcode}] i_sql_rec_count:[{_iSqlRecCount}]\n");

            using (OracleCommand commitCmd = _oraConn.CreateCommand())
            {
                commitCmd.CommandText = "COMMIT";
                commitCmd.ExecuteNonQuery();
            }

            return 0;
        }

        private static int CheckPxxtHospFlowStatus1700()
        {
            string wkHospCnt = string.Empty;
            int iOpHospFlow1700 = 0;
            int iHpHospFlow1700 = 0;
            int iHosp0116 = 0;
            int iHosp08 = 0;

            WriteMsg("========================= \n");
            WriteMsg($" <<< check_pxxt_hosp_flow_status_1700() wk_hosp_id_m :[{_wkHospIdM}] \n");

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = "SELECT CASE WHEN SUBSTR(:wkHospIdM,1,2) BETWEEN '01' AND '16' THEN 1 ELSE 0 END, " +
                                 "SUBSTR(:wkHospIdM,1,2) FROM DUAL";
                cmd.Parameters.Add(new OracleParameter("wkHospIdM", _wkHospIdM));

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        iHosp0116 = reader.GetInt32(0);
                        wkHospCnt = reader.GetString(1);
                    }
                }

                WriteMsg($"     select hosp_id_m:[{_wkHospIdM}] sqlcode :[0] ");
                WriteMsg($"     wk_hosp_cnt     :[{wkHospCnt}] ");
                WriteMsg($"     i_hosp_01_16    :[{iHosp0116}] ");

                cmd.CommandText = "SELECT CASE WHEN SUBSTR(:wkHospIdM,1,2) = '08' THEN 1 ELSE 0 END FROM DUAL";
                cmd.Parameters.Clear();
                cmd.Parameters.Add(new OracleParameter("wkHospIdM", _wkHospIdM));

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        iHosp08 = reader.GetInt32(0);
                    }
                }

                WriteMsg($"     i_hosp_08      :[{iHosp08}] ");

                if (iHosp0116 == 1)
                {
                    WriteMsg("     為醫院層級(院所前二碼為01-16)者 ");
                    WriteMsg($"     wk_hosp_id_m  :[{_wkHospIdM}] ");
                    WriteMsg($"     wk_fee_ym     :[{_wkFeeYm}] ");
                    WriteMsg($"     wk_branch_code:[{_wkBranchCode}] ");

                    if (iHosp08 == 1)
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM PXXT_HOSP_FLOW_STATUS " +
                                         "WHERE FLOW_CODE = 'PB0' " +
                                         "AND hosp_id = :wkHospIdM " +
                                         "AND hosp_data_type = '14' " +
                                         "AND fee_ym = TO_DATE(:wkFeeYm, 'YYYYMMDD') " +
                                         "AND appl_type = '1' " +
                                         "AND branch_code = :wkBranchCode " +
                                         "AND STATUS_CODE = '1700' " +
                                         "AND ROWNUM < 2";
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(new OracleParameter("wkHospIdM", _wkHospIdM));
                        cmd.Parameters.Add(new OracleParameter("wkFeeYm", _wkFeeYm));
                        cmd.Parameters.Add(new OracleParameter("wkBranchCode", _wkBranchCode));

                        using (OracleDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                iOpHospFlow1700 = reader.GetInt32(0);
                            }
                        }

                        WriteMsg($"     SQL301 門診轉檔 sqlcode:[0] ");
                        WriteMsg($"     pass :[{_wkHospIdM}] \n");
                        WriteMsg($"     i_op_hosp_flow_1700:[{iOpHospFlow1700}] \n");
                    }
                    else
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM PXXT_HOSP_FLOW_STATUS " +
                                         "WHERE FLOW_CODE = 'PB0' " +
                                         "AND hosp_id = :wkHospIdM " +
                                         "AND hosp_data_type = '12' " +
                                         "AND fee_ym = TO_DATE(:wkFeeYm, 'YYYYMMDD') " +
                                         "AND appl_type = '1' " +
                                         "AND branch_code = :wkBranchCode " +
                                         "AND STATUS_CODE = '1700' " +
                                         "AND ROWNUM < 2";
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(new OracleParameter("wkHospIdM", _wkHospIdM));
                        cmd.Parameters.Add(new OracleParameter("wkFeeYm", _wkFeeYm));
                        cmd.Parameters.Add(new OracleParameter("wkBranchCode", _wkBranchCode));

                        using (OracleDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                iOpHospFlow1700 = reader.GetInt32(0);
                            }
                        }

                        WriteMsg($"     SQL201 門診轉檔 sqlcode:[0] ");

                        cmd.CommandText = "SELECT COUNT(*) FROM PXXT_HOSP_FLOW_STATUS " +
                                         "WHERE FLOW_CODE = 'PB0' " +
                                         "AND hosp_id = :wkHospIdM " +
                                         "AND hosp_data_type = '22' " +
                                         "AND fee_ym = TO_DATE(:wkFeeYm, 'YYYYMMDD') " +
                                         "AND appl_type = '1' " +
                                         "AND branch_code = :wkBranchCode " +
                                         "AND STATUS_CODE = '1700' " +
                                         "AND ROWNUM < 2";
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(new OracleParameter("wkHospIdM", _wkHospIdM));
                        cmd.Parameters.Add(new OracleParameter("wkFeeYm", _wkFeeYm));
                        cmd.Parameters.Add(new OracleParameter("wkBranchCode", _wkBranchCode));

                        using (OracleDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                iHpHospFlow1700 = reader.GetInt32(0);
                            }
                        }

                        WriteMsg($"     SQL202 住診轉檔 sqlcode:[0] ");

                        if (iHpHospFlow1700 == 0)
                        {
                            WriteMsg($"     i_op_hosp_flow_1700:[{iOpHospFlow1700}] ");
                            WriteMsg($"     i_hp_hosp_flow_1700:[{iHpHospFlow1700}] ");
                            WriteMsg("     清單醫令尚未完成轉檔。return PB0_NOT_FOUND ( continue ) ");
                            WriteMsg("========================= \n");
                            return PB0_NOT_FOUND;
                        }

                        WriteMsg($"     pass :[{_wkHospIdM}] \n");
                        WriteMsg($"     i_op_hosp_flow_1700:[{iOpHospFlow1700}] \n");
                        WriteMsg($"     i_hp_hosp_flow_1700:[{iHpHospFlow1700}] \n");
                    }
                }
            }

            WriteMsg("========================= \n");
            return PB0_FOUND;
        }

        private static void Fun1DelHmDtl()
        {
            WriteMsg($"fun_1_del_hm_dtl  wk_real_hosp_id[{_wkRealHospId}] wk_fee_ym[{_wkFeeYm}] ");

            string sql = $"DELETE FROM ICEI_ASSAY_DL3_DTL \n" +
                        $"  WHERE hosp_id = '{_wkRealHospId}' --1:wk_real_hosp_id \n" +
                        $"    AND fee_ym   = TO_DATE('{_wkFeeYm}','yyyymmdd') --2:wk_fee_ym \n";
            ExecuteSQL(sql);

            sql = "DELETE FROM ICEI_ASSAY_DL3_DATA \n";
            ExecuteSQL(sql);

            sql = "DELETE FROM ICEI_PBA_DL3_DATA \n";
            ExecuteSQL(sql);
        }

        private static void Fun1InitHmMst()
        {
            WriteMsg($"fun_1_init_hm_mst  wk_real_hosp_id[{_wkRealHospId}] wk_fee_ym[{_wkFeeYm}] ");

            string sql = $"DELETE FROM ICEI_ASSAY_DL3_MST  \n" +
                        $"  WHERE hosp_id  = '{_wkRealHospId}'  --1:wk_real_hosp_id  \n" +
                        $"    AND fee_ym   = TO_DATE('{_wkFeeYm}','yyyymmdd') --2:wk_fee_ym  \n";
            ExecuteSQL(sql);

            sql = $"INSERT INTO ICEI_ASSAY_DL3_MST  --fun_1_init_hm_mst \n" +
                 $"       ( branch_code ,      \n" +
                 $"         hosp_id ,          \n" +
                 $"         season_tot ,       \n" +
                 $"         fee_ym ,           \n" +
                 $"         index_type,        \n" +
                 $"         hosp_cnt_type ,    \n" +
                 $"         report_type ,      \n" +
                 $"         appl_qty ,         \n" +
                 $"         assay_qty ,        \n" +
                 $"         audit_qty ,        \n" +
                 $"         txt_date  ,        \n" +
                 $"         PROC_STATUS )      \n" +
                 $"  SELECT '{_wkRealBranchCode}' branch_code  , --wk_real_branch_code   \n" +
                 $"         '{_wkRealHospId}' hosp_id      , --wk_real_hosp_id       \n" +
                 $"         {_wkSeasonTot}   season_tot   , --wk_season_tot         \n" +
                 $"         TO_DATE('{_wkFeeYm}','yyyymmdd') fee_ym ,  --wk_fee_ym \n" +
                 $"         '{_wkIndexType}' index_type   , --wk_index_type         \n" +
                 $"         '{_wkRealHospCntType}' hosp_cnt_type, --wk_real_hosp_cnt_type \n" +
                 $"         4 report_type ,    \n" +
                 $"         0 appl_qty,        \n" +
                 $"         0 assay_qty ,      \n" +
                 $"         0 audit_qty ,      \n" +
                 $"         SYSDATE txt_date , \n" +
                 $"         '1' PROC_STATUS    \n" +
                 $"    FROM DUAL               \n";
            ExecuteSQL(sql);
        }

        private static void Fun1DelHmSts()
        {
            WriteMsg($"fun_1_del_hm_sts wk_real_hosp_id[{_wkRealHospId}] wk_fee_ym[{_wkFeeYm}] wk_index_type:[{_wkIndexType}] ");

            string sql = $"DELETE FROM ICEI_ASSAY_DTL_STS \n" +
                        $"  WHERE hosp_id    = '{_wkRealHospId}' --1:wk_real_hosp_id \n" +
                        $"    AND fee_ym     = TO_DATE('{_wkFeeYm}','yyyymmdd') --2:wk_fee_ym \n" +
                        $"    AND index_type = '{_wkIndexType}' --3:wk_index_type \n";
            ExecuteSQL(sql);
        }

        private static void Fun2DelIdc()
        {
            WriteMsg($"fun_2_del_idc  wk_real_hosp_id[{_wkRealHospId}] wk_fee_ym[{_wkFeeYm}] ");

            string sql = $"DELETE FROM NHI_IDC.ICEE_ASSAY_DL3_DTL \n" +
                        $"  WHERE hosp_id = '{_wkRealHospId}' --1:wk_real_hosp_id \n" +
                        $"    AND fee_ym  = TO_DATE('{_wkFeeYm}','yyyymmdd') --2:wk_fee_ym \n";
            ExecuteSQL(sql);
        }

        private static void Fun4HpInsHm()
        {
            if (_wkSeasonCnt == 0) return;

            if (_wkRealHospCntType != "1" && _wkRealHospCntType != "2" && _wkRealHospCntType != "3" &&
                _wkRealHospCntType != "4" && _wkRealHospCntType != "D" && _wkRealHospCntType != "6" &&
                _wkRealHospCntType != "7" && _wkRealHospCntType != "8")
            {
                return;
            }

            WriteMsg($"fun_4_hp_ins_hm  wk_fee_ym[{_wkFeeYm}] wk_hosp_id[{_wkHospId}] ");

            string sql = "DELETE FROM ICEI_PBA_DL3_DATA \n";
            ExecuteSQL(sql);

            sql = $"INSERT INTO ICEI_PBA_DL3_DATA (  \n" +
                 $"            HOSP_ID,            \n" +
                 $"            HOSP_DATA_TYPE,     \n" +
                 $"            FEE_YM,             \n" +
                 $"            APPL_TYPE,          \n" +
                 $"            APPL_DATE,          \n" +
                 $"            CASE_TYPE,          \n" +
                 $"            SEQ_NO,             \n" +
                 $"            id_aes,                 \n" +
                 $"            BIRTHDAY,           \n" +
                 $"            FUNC_SEQ_NO ,       \n" +
                 $"            ORDER_SEQ_NO,       \n" +
                 $"            ORDER_CODE,         \n" +
                 $"            ORDER_QTY,          \n" +
                 $"            REAL_HOSP_ID  )     \n" +
                 $"     SELECT A.HOSP_ID,          \n" +
                 $"            A.hosp_data_type,   \n" +
                 $"            A.fee_ym,           \n" +
                 $"            A.appl_type,        \n" +
                 $"            A.appl_date,        \n" +
                 $"            A.case_type,        \n" +
                 $"            A.seq_no,           \n" +
                 $"            A.id_aes,               \n" +
                 $"            A.birthday,         \n" +
                 $"            A.func_seq_no,      \n" +
                 $"            B.order_seq_no,     \n" +
                 $"            B.order_code,       \n" +
                 $"            B.order_qty ,       \n" +
                 $"            A.real_hosp_id      \n" +
                 $"       FROM PBAB_HP_DTL  a,PBAB_HP_ORD  b    \n" +
                 $"      WHERE b.fee_ym     = TO_DATE('{_wkFeeYm}','yyyymmdd') --1:wk_fee_ym    \n" +
                 $"        AND (b.hosp_id   = '{_wkHospIdM}' OR                  --2:wk_hosp_id_m \n" +
                 $"            (b.hosp_id   = '1202080010' AND '{_wkHospIdM}' = '1107350015') OR --3:wk_hosp_id_m \n" +
                 $"            (b.hosp_id   = '2121010019' AND '{_wkHospIdM}' = '0141270019'))   --4:wk_hosp_id_m \n" +
                 $"        AND b.appl_date  < LAST_DAY(ADD_MONTHS(b.fee_ym,1))+1        \n" +
                 $"        AND (NVL(a.real_hosp_id,a.hosp_id) = '{_wkRealHospId}'  OR               --5:wk_real_hosp_id \n" +
                 $"            (NVL(a.real_hosp_id,a.hosp_id) = '1202080010' AND '{_wkRealHospId}' = '1107350015') OR  --6:wk_real_hosp_id  \n" +
                 $"            (NVL(a.real_hosp_id,a.hosp_id) = '2121010019' AND '{_wkRealHospId}' = '0141270019'))    --7:wk_real_hosp_id  \n" +
                 $"        AND b.appl_type IN ('1','2')            \n" +
                 $"        AND b.hosp_id    = a.hosp_id            \n" +
                 $"        AND b.hosp_data_type = a.hosp_data_type \n" +
                 $"        AND b.fee_ym     = a.fee_ym             \n" +
                 $"        AND b.appl_type  = a.appl_type          \n" +
                 $"        AND b.appl_date  = a.appl_date          \n" +
                 $"        AND b.case_type  = a.case_type          \n" +
                 $"        AND b.seq_no     = a.seq_no             \n" +
                 $"        AND a.hosp_data_type ='22'              \n" +
                 $"        AND b.order_code IN ('64164B','64169B','64202B','64162B','64170B','64258B','64201B')  \n" +
                 $"      --AND a.case_type IN ('1','5')            \n" +
                 $"        AND a.appl_type IN ('1','2')            \n" +
                 $"        AND NVL(a.TW_DRGS_SUIT_MARK,'-') <> '9' \n" +
                 $"        AND ( ( b.ORDER_TYPE !='4' ) OR         \n" +
                 $"              ( b.ORDER_TYPE  ='4' AND          \n" +
                 $"                (( a.case_type IN ('2','5')) OR \n" +
                 $"                 ( a.case_type = '4' AND a.PAY_TYPE='9' ) OR  \n" +
                 $"                 ( a.case_type = '4' AND a.PAT_SOURCE IN ('N','R','C')) OR  \n" +
                 $"                 ( a.case_type = '6' )          \n" +
                 $"            )))    \n";
            ExecuteSQL(sql);

            sql = $"INSERT INTO ICEI_ASSAY_DL3_DTL --fun_3_op_ins_hm \n" +
                 $"          ( branch_code ,      \n" +
                 $"            hosp_id ,          \n" +
                 $"            appl_hosp_id,      \n" +
                 $"            real_hosp_id,      \n" +
                 $"            season_tot ,       \n" +
                 $"            fee_ym ,           \n" +
                 $"            exe_ym ,           \n" +
                 $"            index_type,        \n" +
                 $"            hosp_cnt_type ,    \n" +
                 $"            id_aes ,               \n" +
                 $"            id_idc_aes ,           \n" +
                 $"            order_code ,       \n" +
                 $"            report_type ,      \n" +
                 $"            data_type ,        \n" +
                 $"            order_qty ,        \n" +
                 $"            txt_date  )        \n" +
                 $"     SELECT '{_wkBranchCode}' branch_code,   --1:wk_branch_code  \n" +
                 $"            '{_wkRealHospId}' hosp_id,       --2:wk_real_hosp_id \n" +
                 $"            hosp_id appl_hosp_id,                   \n" +
                 $"            real_hosp_id,                           \n" +
                 $"            '{_wkSeasonTot}' season_tot ,   --3:wk_season_tot   \n" +
                 $"            fee_ym,                                 \n" +
                 $"            fee_ym exe_ym ,                         \n" +
                 $"            '{_wkIndexType}' index_type,    --4:wk_index_type   \n" +
                 $"            '{_wkRealHospCntType}' hosp_cnt_type, --5:wk_real_hosp_cnt_type\n" +
                 $"            id_aes,                                \n" +
                 $"            {_wkConvIdc}(id_aes)),            --6:wk_conv_idc  \n" +
                 $"            order_code ,                       \n" +
                 $"            4 report_type ,                    \n" +
                 $"            'HP' data_type ,                   \n" +
                 $"            SUM(NVL(order_qty,1)) order_qty,   \n" +
                 $"            SYSDATE txt_date                   \n" +
                 $"       FROM ICEI_PBA_DL3_DATA                      \n" +
                 $"   GROUP BY hosp_id ,real_hosp_id, fee_ym, id_aes, order_code  \n";
            ExecuteSQL(sql);
        }

        private static void Fun5AssayInsHm()
        {
            if (_wkSeasonCnt == 0) return;

            WriteMsg($"fun_5_assay_ins_hm wk_hosp_id[{_wkHospId}] wk_fee_ym[{_wkFeeYm}] wk_season_tot:[{_wkSeasonTot}] ");

            string sql = "DELETE FROM ICEI_ASSAY_DL3_DATA \n";
            ExecuteSQL(sql);

            sql = $"INSERT INTO ICEI_ASSAY_DL3_DATA (   \n" +
                 $"              hosp_id,            \n" +
                 $"              hosp_data_type,     \n" +
                 $"              fee_ym,             \n" +
                 $"              appl_type,          \n" +
                 $"              appl_date,          \n" +
                 $"              case_type,          \n" +
                 $"              seq_no,             \n" +
                 $"              ORDER_SEQ_NO,       \n" +
                 $"              order_code,         \n" +
                 $"              id_aes,                 \n" +
                 $"              birthday,           \n" +
                 $"              func_seq_no,        \n" +
                 $"              source_type,        \n" +
                 $"              assay_upload_date,  \n" +
                 $"              case_seq_no,        \n" +
                 $"              case_report_type,   \n" +
                 $"              real_inspect_date,  \n" +
                 $"              txt_mark        )   \n" +
                 $"       SELECT hosp_id,            \n" +
                 $"              hosp_data_type,     \n" +
                 $"              fee_ym,             \n" +
                 $"              appl_type,          \n" +
                 $"              appl_date,          \n" +
                 $"              case_type,          \n" +
                 $"              seq_no,             \n" +
                 $"              ORDER_SEQ_NO,       \n" +
                 $"              order_code,         \n" +
                 $"              id_aes,                 \n" +
                 $"              birthday,           \n" +
                 $"              func_seq_no,        \n" +
                 $"              source_type,        \n" +
                 $"              assay_upload_date,  \n" +
                 $"              case_seq_no,        \n" +
                 $"              case_report_type,   \n" +
                 $"              real_inspect_date,  \n" +
                 $"              txt_mark            \n" +
                 $"         FROM ICEI_ASSAY_DTL_DAY_AES_V_{_wkViewMonth} a     --1:wk_view_month   \n" +
                 $"        WHERE a.fee_ym BETWEEN ADD_MONTHS(TO_DATE('{_wkFeeYm}','yyyymmdd'),-3) --2:wk_fee_ym \n" +
                 $"                           AND TO_DATE('{_wkFeeYm}','yyyymmdd') --3:wk_fee_ym \n" +
                 $"          AND ( a.hosp_id  IN {_sInAssayHosp} OR  --4:s_in_assay_hosp          \n" +
                 $"               ( '{_wkHospIdM}' = '1107350015' AND a.hosp_id   = '1202080010' ) OR  --5:wk_hosp_id_m   \n" +
                 $"               ( '{_wkHospIdM}' = '0141270019' AND a.hosp_id   = '2121010019' ) )   --6:wk_hosp_id_m   \n" +
                 $"          AND EXISTS ( SELECT 1                                    \n" +
                 $"                         FROM ICEI_ASSAY_DL3_DTL b                  \n" +
                 $"                        WHERE b.fee_ym     = TO_DATE('{_wkFeeYm}','yyyymmdd') --7:wk_fee_ym \n" +
                 $"                          AND b.hosp_id    = '{_wkRealHospId}'  --8:wk_real_hosp_id  \n" +
                 $"                          AND b.index_type = '{_wkIndexType}'  --9:wk_index_type  \n" +
                 $"                          AND b.data_type  = 'HP'                  \n" +
                 $"                          AND b.id_aes         = a.id_aes                  \n" +
                 $"                          AND b.order_code = a.order_code )        \n" +
                 $"          AND a.case_report_type = '4'                             \n" +
                 $"          AND a.case_seq_no IN (1,2,3,4,5)                         \n" +
                 $"          AND order_code IN ('64164B','64169B','64202B','64162B','64170B','64258B','64201B')  \n";
            ExecuteSQL(sql);

            sql = $"INSERT INTO ICEI_ASSAY_DL3_DTL   --fun_5_assay_ins_hm \n" +
                 $"      ( branch_code ,       \n" +
                 $"        hosp_id ,           \n" +
                 $"        season_tot ,        \n" +
                 $"        fee_ym ,            \n" +
                 $"        exe_ym ,            \n" +
                 $"        index_type,         \n" +
                 $"        hosp_cnt_type ,     \n" +
                 $"        id_aes ,                \n" +
                 $"        id_idc_aes ,            \n" +
                 $"        order_code ,        \n" +
                 $"        report_type ,       \n" +
                 $"        data_type ,         \n" +
                 $"        order_qty ,         \n" +
                 $"        appl_hosp_id,       \n" +
                 $"        real_hosp_id,       \n" +
                 $"        txt_date  )         \n" +
                 $" SELECT '{_wkBranchCode}' branch_code,   --1:wk_branch_code  \n" +
                 $"        '{_wkRealHospId}' hosp_id,       --2:wk_real_hosp_id \n" +
                 $"        '{_wkSeasonTot}' season_tot,    --3:wk_season_tot   \n" +
                 $"        TO_DATE('{_wkFeeYm}','YYYYMMDD') fee_ym, --4:wk_fee_ym  \n" +
                 $"        fee_ym exe_ym,      \n" +
                 $"        '3' index_type,     \n" +
                 $"        '{_wkRealHospCntType}' hosp_cnt_type, --5:wk_real_hosp_cnt_type \n" +
                 $"        id_aes,                 \n" +
                 $"        {_wkConvIdc}(id_aes)),            --6:wk_conv_idc  \n" +
                 $"        order_code,         \n" +
                 $"        case_report_type report_type ,       \n" +
                 $"        source_type data_type,               \n" +
                 $"        NVL(FLOOR(SUM( CASE WHEN case_seq_cnt > order_qty THEN order_qty ELSE case_seq_cnt END )),0)  order_qty,  \n" +
                 $"        '{_wkHospIdM}' appl_hosp_id,  --7:wk_hosp_id_m    \n" +
                 $"        '{_wkRealHospId}' real_hosp_id,  --8:wk_real_hosp_id \n" +
                 $"        SYSDATE txt_date   \n" +
                 $"  FROM (  \n" +
                 $"     SELECT A.fee_ym, MIN(assay_upload_date) assay_upload_date,  \n" +
                 $"            DECODE(LEAST(SUM(CASE WHEN A.case_seq_no = 1 THEN 1 ELSE 0 END),1)+    \n" +
                 $"                   LEAST(SUM(CASE WHEN A.case_seq_no = 2 THEN 2 ELSE 0 END),2)+    \n" +
                 $"                   LEAST(SUM(CASE WHEN A.case_seq_no = 3 THEN 3 ELSE 0 END),3)+    \n" +
                 $"                   LEAST(SUM(CASE WHEN A.case_seq_no = 4 THEN 4 ELSE 0 END),4)+    \n" +
                 $"                   LEAST(SUM(CASE WHEN A.case_seq_no = 5 THEN 5 ELSE 0 END),5),    \n" +
                 $"                   10,1,11,0,15,2,0)  case_seq_cnt ,  \n" +
                 $"            MIN(B.order_qty) order_qty,  \n" +
                 $"            A.id_aes,               \n" +
                 $"            A.ORDER_CODE,       \n" +
                 $"            A.source_type,      \n" +
                 $"            A.case_report_type  \n" +
                 $"       FROM ICEI_ASSAY_DL3_DATA A,  \n" +
                 $"            ICEI_PBA_DL3_DATA   B,  \n" +
                 $"         (  SELECT hosp_data_type,fee_ym,appl_type,appl_date,case_type,seq_no,ORDER_SEQ_NO,order_code,id_aes,birthday,func_seq_no,source_type  \n" +
                 $"              FROM (  \n" +
                 $"                SELECT A.hosp_id,A.hosp_data_type,A.fee_ym,A.appl_type,A.appl_date,A.case_type,    \n" +
                 $"                       A.seq_no,A.id_aes,A.birthday,A.ORDER_SEQ_NO,A.order_code,A.func_seq_no,A.source_type,  \n" +
                 $"                       MIN(A.assay_upload_date) assay_upload_date  \n" +
                 $"                  FROM ICEI_ASSAY_DL3_DATA A                           \n" +
                 $"                 GROUP BY A.hosp_id,A.hosp_data_type,A.fee_ym,A.appl_type,A.appl_date,A.case_type, \n" +
                 $"                       A.seq_no,A.id_aes,A.birthday,A.ORDER_SEQ_NO,A.order_code,id_aes,birthday,A.func_seq_no,A.source_type )  \n" +
                 $"          ) C     \n" +
                 $"      WHERE A.case_report_type = '4'                  \n" +
                 $"        AND A.case_seq_no IN (1,2,3,4,5)              \n" +
                 $"        AND (A.txt_mark IS NULL OR A.txt_mark NOT IN ('1','2')) --/* 2018/08/27 改排除1及2，空值及3及4均納入計算*/  \n" +
                 $"        AND A.hosp_data_type      = B.hosp_data_type  \n" +
                 $"        AND A.fee_ym  BETWEEN ADD_MONTHS(B.fee_ym,-3) AND B.fee_ym     --/* 2018/08/21  勾稽範圍擴大往前3個月*/    \n" +
                 $"        AND A.assay_upload_date   < LAST_DAY(ADD_MONTHS(b.fee_ym,1))+1  --/* 2018/08/21 以申報案件之費用年月與上傳案件之上傳日期來判斷*/  \n" +
                 $"        AND A.order_code          = B.order_code      \n" +
                 $"        AND (( A.source_type      = 'M'               \n" +
                 $"               AND A.appl_type    = B.appl_type       \n" +
                 $"            -- AND A.appl_date    = B.appl_date 2019/11/28 不勾申報 appl_date  \n" +
                 $"               AND A.case_type    = B.case_type       \n" +
                 $"               AND A.seq_no       = B.seq_no          \n" +
                 $"               AND A.order_seq_no = B.order_seq_no    \n" +
                 $"               AND A.fee_ym       = B.fee_ym          \n" +
                 $"             )                                        \n" +
                 $"             OR                                       \n" +
                 $"             ( A.source_type      = 'D'               \n" +
                 $"               AND A.id_aes           = B.id_aes              \n" +
                 $"               AND A.BIRTHDAY     = B.BIRTHDAY        \n" +
                 $"               AND a.func_seq_no  = B.func_seq_no     \n" +
                 $"               AND A.fee_ym BETWEEN ADD_MONTHS(B.fee_ym,-3) AND B.fee_ym \n" +
                 $"             ))                                       \n" +
                 $"        AND A.hosp_data_type      = C.hosp_data_type  \n" +
                 $"        AND A.fee_ym              = C.fee_ym          \n" +
                 $"        AND A.order_code          = C.order_code      \n" +
                 $"        AND (( A.source_type      = 'M'               \n" +
                 $"               AND A.appl_type    = C.appl_type       \n" +
                 $"               AND A.appl_date    = C.appl_date       \n" +
                 $"               AND A.case_type    = C.case_type       \n" +
                 $"               AND A.seq_no       = C.seq_no          \n" +
                 $"               AND A.order_seq_no = C.order_seq_no    \n" +
                 $"             )                                        \n" +
                 $"             OR                                       \n" +
                 $"             ( A.source_type      = 'D'               \n" +
                 $"             AND A.id_aes             = C.id_aes              \n" +
                 $"             AND A.BIRTHDAY       = C.BIRTHDAY        \n" +
                 $"             AND a.func_seq_no    = C.func_seq_no  )) \n" +
                 $"  GROUP BY A.hosp_data_type,A.fee_ym,A.appl_type,A.appl_date,A.case_type,A.seq_no,A.id_aes,  \n" +
                 $"        A.birthday,A.order_seq_no,A.order_code,       \n" +
                 $"        A.case_report_type,A.real_inspect_date,A.source_type  )  \n" +
                 $"  GROUP BY fee_ym,id_aes, order_code,case_report_type,source_type    \n";
            ExecuteSQL(sql);
        }

        private static void Fun6InsIdc()
        {
            if (_wkSeasonCnt == 0) return;

            WriteMsg($"fun_6_ins_idc wk_hosp_id[{_wkHospId}] wk_fee_ym[{_wkFeeYm}] wk_season_tot:[{_wkSeasonTot}] ");

            string sql = $"INSERT INTO NHI_IDC.ICEE_ASSAY_DL3_DTL  --fun_6_ins_idc \n" +
                        $"       ( branch_code ,    \n" +
                        $"         hosp_id ,        \n" +
                        $"         season_tot ,     \n" +
                        $"         fee_ym ,         \n" +
                        $"         index_type,      \n" +
                        $"         oipd_type,       \n" +
                        $"         hosp_cnt_type ,  \n" +
                        $"         id_aes ,             \n" +
                        $"         order_code ,     \n" +
                        $"         report_type ,    \n" +
                        $"         appl_qty ,       \n" +
                        $"         assay_qty ,      \n" +
                        $"         assay_qty_m0 ,   \n" +
                        $"         assay_qty_m1 ,   \n" +
                        $"         assay_qty_m2 ,   \n" +
                        $"         assay_qty_m3 ,   \n" +
                        $"         audit_qty ,      \n" +
                        $"         appl_hosp_id,    \n" +
                        $"         real_hosp_id,    \n" +
                        $"         txt_date  )      \n" +
                        $"  SELECT '{_wkBranchCode}' branch_code, --1:wk_branch_code \n" +
                        $"         hosp_id ,        \n" +
                        $"         season_tot ,     \n" +
                        $"         fee_ym ,         \n" +
                        $"         index_type,      \n" +
                        $"         oipd_type,       \n" +
                        $"         hosp_cnt_type ,  \n" +
                        $"         id_idc_aes id_aes,       \n" +
                        $"         order_code ,     \n" +
                        $"         report_type ,    \n" +
                        $"         appl_qty,        \n" +
                        $"         assay_qty ,      \n" +
                        $"         assay_qty_m0 ,   \n" +
                        $"         assay_qty_m1 ,   \n" +
                        $"         assay_qty_m2 ,   \n" +
                        $"         assay_qty_m3 ,   \n" +
                        $"         CASE WHEN appl_qty < assay_qty          \n" +
                        $"              THEN appl_qty                      \n" +
                        $"              ELSE assay_qty END audit_qty ,     \n" +
                        $"         '{_wkHospIdM}' appl_hosp_id, --2:wk_hosp_id_m     \n" +
                        $"         '{_wkRealHospId}' real_hosp_id, --3:wk_real_hosp_id  \n" +
                        $"         SYSDATE txt_date                        \n" +
                        $"    FROM (  SELECT hosp_id ,                     \n" +
                        $"                   season_tot ,                  \n" +
                        $"                   fee_ym ,                      \n" +
                        $"                   index_type,                   \n" +
                        $"                   '2' oipd_type,                \n" +
                        $"                   hosp_cnt_type ,               \n" +
                        $"                   id_idc_aes ,                      \n" +
                        $"                   order_code ,                  \n" +
                        $"                   report_type ,                 \n" +
                        $"                   SUM(CASE WHEN UPPER(data_type) IN ('HP')  \n" +
                        $"                            THEN order_qty       \n" +
                        $"                            ELSE 0               \n" +
                        $"                             END ) appl_qty,     \n" +
                        $"                   SUM(CASE WHEN exe_ym = fee_ym \n" +
                        $"                             AND UPPER(data_type) IN ('D','M') \n" +
                        $"                            THEN order_qty       \n" +
                        $"                            ELSE 0               \n" +
                        $"                             END ) assay_qty_m0, \n" +
                        $"                   SUM(CASE WHEN exe_ym = ADD_MONTHS(fee_ym,-1) \n" +
                        $"                             AND UPPER(data_type) IN ('D','M') \n" +
                        $"                            THEN order_qty       \n" +
                        $"                            ELSE 0               \n" +
                        $"                             END ) assay_qty_m1, \n" +
                        $"                   SUM(CASE WHEN exe_ym = ADD_MONTHS(fee_ym,-2) \n" +
                        $"                             AND UPPER(data_type) IN ('D','M') \n" +
                        $"                            THEN order_qty       \n" +
                        $"                            ELSE 0               \n" +
                        $"                             END ) assay_qty_m2, \n" +
                        $"                   SUM(CASE WHEN exe_ym = ADD_MONTHS(fee_ym,-3) \n" +
                        $"                             AND UPPER(data_type) IN ('D','M') \n" +
                        $"                            THEN order_qty       \n" +
                        $"                            ELSE 0               \n" +
                        $"                             END ) assay_qty_m3, \n" +
                        $"                   SUM(CASE WHEN exe_ym BETWEEN ADD_MONTHS(fee_ym,-3) AND fee_ym \n" +
                        $"                             AND UPPER(data_type) IN ('D','M') \n" +
                        $"                            THEN order_qty       \n" +
                        $"                            ELSE 0               \n" +
                        $"                             END ) assay_qty     \n" +
                        $"              FROM ICEI_ASSAY_DL3_DTL            \n" +
                        $"             WHERE hosp_id  = '{_wkRealHospId}'  --4:wk_real_hosp_id  \n" +
                        $"               AND fee_ym   = TO_DATE('{_wkFeeYm}','yyyymmdd') --5:wk_fee_ym  \n" +
                        $"               AND index_type = '{_wkIndexType}'  --6:wk_index_type      \n" +
                        $"            GROUP BY hosp_id , season_tot ,fee_ym ,index_type,hosp_cnt_type , id_idc_aes ,order_code , report_type ) \n";
            ExecuteSQL(sql);
        }

        private static void Fun7InsHmMst()
        {
            if (_wkSeasonCnt == 0) return;

            WriteMsg($"fun_7_ins_hm_mst wk_real_hosp_id[{_wkRealHospId}] wk_fee_ym[{_wkFeeYm}] wk_index_type:[{_wkIndexType}] ");

            string sql = $"DELETE FROM ICEI_ASSAY_DL3_MST  \n" +
                        $"  WHERE hosp_id  = '{_wkRealHospId}'  --1:wk_real_hosp_id  \n" +
                        $"    AND fee_ym   = TO_DATE('{_wkFeeYm}','yyyymmdd') --2:wk_fee_ym  \n";
            ExecuteSQL(sql);

            sql = $"INSERT INTO ICEI_ASSAY_DL3_MST  --fun_7_ins_hm_mst \n" +
                 $"       ( branch_code ,      \n" +
                 $"         hosp_id ,          \n" +
                 $"         season_tot ,       \n" +
                 $"         fee_ym ,           \n" +
                 $"         index_type,        \n" +
                 $"         hosp_cnt_type ,    \n" +
                 $"         report_type ,      \n" +
                 $"         appl_qty ,         \n" +
                 $"         assay_qty ,        \n" +
                 $"         audit_qty ,        \n" +
                 $"         txt_date  ,        \n" +
                 $"         PROC_STATUS )      \n" +
                 $"  SELECT branch_code,       \n" +
                 $"         hosp_id ,          \n" +
                 $"         season_tot ,       \n" +
                 $"         fee_ym ,           \n" +
                 $"         index_type,        \n" +
                 $"         hosp_cnt_type ,    \n" +
                 $"         report_type ,      \n" +
                 $"         SUM(appl_qty),     \n" +
                 $"         SUM(assay_qty) ,   \n" +
                 $"         SUM(CASE WHEN appl_qty < assay_qty THEN appl_qty ELSE assay_qty END) audit_qty , \n" +
                 $"         SYSDATE txt_date,  \n" +
                 $"         'Y' PROC_STATUS    \n" +
                 $"    FROM ( SELECT branch_code,hosp_id , season_tot ,fee_ym  ,index_type,hosp_cnt_type ,report_type, id_aes,order_code , \n" +
                 $"                  SUM(appl_qty) appl_qty,SUM(assay_qty) assay_qty \n" +
                 $"             FROM NHI_IDC.ICEE_ASSAY_DL3_DTL  \n" +
                 $"            WHERE hosp_id  = '{_wkRealHospId}'  --1:wk_real_hosp_id  \n" +
                 $"              AND fee_ym   = TO_DATE('{_wkFeeYm}','yyyymmdd') --2:wk_fee_ym  \n" +
                 $"              AND index_type = '{_wkIndexType}'  --3:wk_index_type      \n" +
                 $"         GROUP BY branch_code,hosp_id , season_tot ,fee_ym  ,index_type,hosp_cnt_type ,report_type, id_aes,order_code ) \n" +
                 $"  GROUP BY branch_code, hosp_id , season_tot ,fee_ym ,index_type ,hosp_cnt_type,report_type  \n";
            ExecuteSQL(sql);

            if (_iSqlRecCount == 0)
            {
                sql = $"INSERT INTO ICEI_ASSAY_DL3_MST  --fun_7_ins_hm_mst \n" +
                     $"       ( branch_code ,      \n" +
                     $"         hosp_id ,          \n" +
                     $"         season_tot ,       \n" +
                     $"         fee_ym ,           \n" +
                     $"         index_type,        \n" +
                     $"         hosp_cnt_type ,    \n" +
                     $"         report_type ,      \n" +
                     $"         appl_qty ,         \n" +
                     $"         assay_qty ,        \n" +
                     $"         audit_qty ,        \n" +
                     $"         txt_date  ,        \n" +
                     $"         PROC_STATUS )      \n" +
                     $"  SELECT '{_wkRealBranchCode}' branch_code, --wk_real_branch_code \n" +
                     $"         '{_wkRealHospId}' hosp_id ,    --wk_real_hosp_id     \n" +
                     $"         {_wkSeasonTot}   season_tot , --wk_season_tot  \n" +
                     $"         TO_DATE('{_wkFeeYm}','yyyymmdd') fee_ym ,  --wk_fee_ym \n" +
                     $"         '{_wkIndexType}' index_type,   --wk_index_type \n" +
                     $"         '{_wkRealHospCntType}' hosp_cnt_type, --wk_real_hosp_cnt_type    \n" +
                     $"         4 report_type ,    \n" +
                     $"         0 appl_qty,        \n" +
                     $"         0 assay_qty ,      \n" +
                     $"         0 audit_qty ,      \n" +
                     $"         SYSDATE txt_date , \n" +
                     $"         'Y' PROC_STATUS    \n" +
                     $"    FROM DUAL               \n";
                ExecuteSQL(sql);
            }
        }

        private static void Fun8InsHmSts()
        {
            WriteMsg($"fun_8_ins_hm_sts wk_real_hosp_id[{_wkRealHospId}] wk_fee_ym[{_wkFeeYm}] wk_index_type:[{_wkIndexType}] ");

            string sql = $"INSERT INTO ICEI_ASSAY_DTL_STS  --fun_8_ins_hm_sts \n" +
                        $"       ( BRANCH_CODE ,      \n" +
                        $"         HOSP_ID ,          \n" +
                        $"         FEE_YM ,           \n" +
                        $"         INDEX_TYPE,        \n" +
                        $"         HOSP_CNT_TYPE ,    \n" +
                        $"         ORDER_CODE ,       \n" +
                        $"         REPORT_TYPE ,      \n" +
                        $"         OIPD_TYPE ,        \n" +
                        $"         SEASON_TOT ,       \n" +
                        $"         APPL_QTY ,         \n" +
                        $"         AUDIT_QTY ,        \n" +
                        $"         ASSAY_QTY ,        \n" +
                        $"         ASSAY_QTY_M0 ,     \n" +
                        $"         ASSAY_QTY_M1 ,     \n" +
                        $"         ASSAY_QTY_M2 ,     \n" +
                        $"         ASSAY_QTY_M3 ,     \n" +
                        $"         TXT_DATE  )        \n" +
                        $"  SELECT branch_code,       \n" +
                        $"         hosp_id ,          \n" +
                        $"         fee_ym ,           \n" +
                        $"         index_type,        \n" +
                        $"         hosp_cnt_type ,    \n" +
                        $"         order_code ,       \n" +
                        $"         report_type ,      \n" +
                        $"         OIPD_TYPE ,        \n" +
                        $"         season_tot ,       \n" +
                        $"         SUM(appl_qty)  appl_qty, \n" +
                        $"         SUM(audit_qty) audit_qty, \n" +
                        $"         SUM(assay_qty) assay_qty , \n" +
                        $"         SUM(assay_qty_m0) assay_qty_m0 , \n" +
                        $"         SUM(assay_qty_m1) assay_qty_m1 , \n" +
                        $"         SUM(assay_qty_m2) assay_qty_m2 , \n" +
                        $"         SUM(assay_qty_m3) assay_qty_m3 , \n" +
                        $"         SYSDATE txt_date   \n" +
                        $"    FROM NHI_IDC.ICEE_ASSAY_DL3_DTL  \n" +
                        $"   WHERE hosp_id    = '{_wkRealHospId}'  --1:wk_real_hosp_id  \n" +
                        $"     AND fee_ym     = TO_DATE('{_wkFeeYm}','yyyymmdd') --2:wk_fee_ym  \n" +
                        $"     AND index_type = '{_wkIndexType}'  --3:wk_index_type      \n" +
                        $" GROUP BY branch_code, hosp_id ,fee_ym ,index_type, hosp_cnt_type,order_code,report_type,OIPD_TYPE,season_tot \n";
            ExecuteSQL(sql);
        }

        private static void Fun9UpdateHmIceiAssayDtl()
        {
            int wkAuditKeenQry = 0;

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = "SELECT SUM(audit_qty) " +
                                 "FROM ICEI_ASSAY_DL3_MST " +
                                 "WHERE hosp_id = :wkHospId " +
                                 "AND fee_ym = TO_DATE(:wkFeeYm,'yyyymmdd') " +
                                 "AND season_tot = :wkSeasonTot";
                cmd.Parameters.Add(new OracleParameter("wkHospId", _wkHospId));
                cmd.Parameters.Add(new OracleParameter("wkFeeYm", _wkFeeYm));
                cmd.Parameters.Add(new OracleParameter("wkSeasonTot", _wkSeasonTot));

                try
                {
                    object result = cmd.ExecuteScalar();
                    if (result != DBNull.Value)
                    {
                        wkAuditKeenQry = Convert.ToInt32(result);
                    }
                }
                catch (OracleException ex)
                {
                    string msg = $"fun_9_update_hm_icei_assay_dtl sql_1 wk_hosp_id:[{_wkHospId}] wk_fee_ym:[{_wkFeeYm}] wk_season_tot:[{_wkSeasonTot}] code={ex.ErrorCode}";
                    Console.WriteLine(msg);
                    _logger.Error(msg);
                    _proList.exitCode = 60;
                    _proList.message = msg;
                    throw;
                }

                cmd.CommandText = "UPDATE ICEI_ASSAY_DTL SET " +
                                 "keen_qty = NVL(:wkAuditKeenQry,0), " +
                                 "CTMRI_U_P1 = 15, " +
                                 "CTMRI_U_P2 = 7, " +
                                 "CTMRI_U_P3 = 2, " +
                                 "pay_amt = NVL(upld_basic_fee,0)+ " +
                                 "( NVL(mds_qty,0)*NVL(MDS_U_P,0))+ " +
                                 "((NVL(report_qty1,0)*NVL(REPORT_U_P1,0))+ " +
                                 "(NVL(report_qty2,0)+NVL(report_qty3,0))*NVL(REPORT_U_P2,0) " +
                                 ")*(CASE WHEN upld_rate > 0.5 THEN 1 ELSE 0 END)+ " +
                                 "NVL(KEEN_QTY,0)*NVL(KEEN_U_P,0)+ " +
                                 "NVL(REF_IN_QTY,0)*NVL(REF_IN_U_P,0)+ " +
                                 "NVL(REF_OUT_QTY,0)*NVL(REF_OUT_U_P,0)+ " +
                                 "NVL(HOME_QTY,0)*NVL(HOME_U_P,0)+ " +
                                 "NVL(SPEC_QTY,0)*NVL(SPEC_U_P,0)+ " +
                                 "NVL(assay_qty,0)*NVL(assay_u_p,0)+ " +
                                 "NVL(assay_qty2,0)*NVL(assay_u_p2,0)+ " +
                                 "NVL(CTMRI_QTY1,0)*NVL(CTMRI_U_P1,15)+ " +
                                 "NVL(CTMRI_QTY2,0)*NVL(CTMRI_U_P2,7)+ " +
                                 "NVL(CTMRI_QTY3,0)*NVL(CTMRI_U_P3,2), " +
                                 "txt_date = TRUNC(SYSDATE) " +
                                 "WHERE hosp_id = :wkRealHospId " +
                                 "AND fee_ym = TO_DATE(:wkFeeYm,'YYYYMMDD') " +
                                 "AND season_tot = :wkSeasonTot";
                cmd.Parameters.Clear();
                cmd.Parameters.Add(new OracleParameter("wkAuditKeenQry", wkAuditKeenQry));
                cmd.Parameters.Add(new OracleParameter("wkRealHospId", _wkRealHospId));
                cmd.Parameters.Add(new OracleParameter("wkFeeYm", _wkFeeYm));
                cmd.Parameters.Add(new OracleParameter("wkSeasonTot", _wkSeasonTot));

                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (OracleException ex)
                {
                    string msg = $"fun_9_update_hm_icei_assay_dtl sql_2 wk_real_hosp_id:[{_wkRealHospId}] wk_fee_ym:[{_wkFeeYm}] wk_season_tot:[{_wkSeasonTot}] code={ex.ErrorCode}";
                    Console.WriteLine(msg);
                    _logger.Error(msg);
                    _proList.exitCode = 61;
                    _proList.message = msg;
                    throw;
                }
            }
        }
        #endregion

        #region Helper Classes
        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }
        #endregion
    }
}
```