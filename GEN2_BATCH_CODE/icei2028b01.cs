```csharp
/*******************************************************************
程式代碼：icei2028b01
程式名稱：每季給付金額上限計算
功能簡述：計算醫療院所每季給付金額上限
參    數：
參數一：程式代號 暫付月 醫事機構代碼(選項)
範例一：icei2028b01 20190101 3501120157
讀取檔案：無
異動檔案：無
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

namespace icei2028b01
{
    public class icei2028b01
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

        /* ---------- structs ---------- */
        private class SQL100
        {
            public string rowid { get; set; } = string.Empty;
            public string hospId { get; set; } = string.Empty;
            public string feeSDate { get; set; } = string.Empty;
            public string feeEDate { get; set; } = string.Empty;
            public string applRate { get; set; } = string.Empty;
            public int monthlyFee { get; set; }
            public int paymentFee { get; set; }
            public int payCeiling { get; set; }
            public string payCeilingMark { get; set; } = string.Empty;
        }

        private class SQL200
        {
            public string memo { get; set; } = string.Empty;
        }

        /* ---------- variables ---------- */
        private static int _iCount = 0;
        private static int _hospCnt = 0;
        private static string _sHospCntType = string.Empty;
        private static int _isNullRecCount = 0;
        private static int _iPayCeiling = 0;
        private static int _iMonthlyFee = 0;

        private static string _sPreHospId = string.Empty;
        private static string _sInputHospId = string.Empty;
        private static string _inputFeeYm = string.Empty;
        private static string _sPayCeilingType = string.Empty;

        private static string _sHospId = string.Empty;
        private static int _iTotMonthlyFee = 0;
        private static int _iTotPaymentFee = 0;

        private static int _rtnCode = 0;

        private static int _iLoopMax = 100; // 同一院所最多重試次數
        private static int _iLoopCount = 0;
        private static int _iPayCeiling1 = 0;
        private static int _iPayCeiling2 = 0;
        private static int _iCPayAmt = 0;
        private static int _iFPayAmt = 0;

        private static int _iFTotPayAmt = 0;

        private static string _msg = string.Empty;

        private static int _iRecCount = 0;

        private static int _iMsg = 0;

        /* ---------- Main ---------- */
        static void Main(string[] args)
        {
            try
            {
                // PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                CheckArg(args);

                // 清除上次執行 icei2028的結果
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("UPDATE ICEI_CHT_FEE A");
                    strSQL.AppendLine("   SET PAY_CEILING = NULL,");
                    strSQL.AppendLine("       MONTHLY_FEE = PAYMENT_FEE,");
                    strSQL.AppendLine("       PAY_CEILING_MARK = NULL");
                    strSQL.AppendLine(" WHERE TO_DATE(:inputFeeYm,'YYYYMMDD')");
                    strSQL.AppendLine("       BETWEEN TO_DATE(TO_CHAR(A.FEE_S_DATE,'YYYYMM')||'01','YYYYMMDD')");
                    strSQL.AppendLine("           AND TO_DATE(TO_CHAR(A.FEE_E_DATE,'YYYYMM')||'01','YYYYMMDD')");
                    strSQL.AppendLine("   AND HOSP_ID = DECODE(UPPER(:sInputHospId),'ALL',HOSP_ID,:sInputHospId)");
                    strSQL.AppendLine("   AND PAY_CEILING_MARK = 'Y'");
                    cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                    cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));

                    cmd.CommandText = strSQL.ToString();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (_iMsg == 1 || cmd.ExecuteNonQuery() != 0)
                    {
                        Console.WriteLine($"    update icei_cht_fee PAY_CEILING input_fee_ym[{_inputFeeYm}] s_input_hosp_id[{_sInputHospId}] sqlcode[0] rec[{rowsAffected}]");
                    }
                }

                // 清除上次執行 icei2028的結果
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("DELETE FROM ICEI_AUDIT_DTL");
                    strSQL.AppendLine(" WHERE DATA_TYPE = 'F'");
                    strSQL.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                    strSQL.AppendLine("   AND HOSP_ID = DECODE(UPPER(:sInputHospId),'ALL',HOSP_ID,:sInputHospId)");
                    cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                    cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));

                    cmd.CommandText = strSQL.ToString();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (_iMsg == 1 || cmd.ExecuteNonQuery() != 0)
                    {
                        Console.WriteLine($"    delete icei_audit_dtl data_type='F' input_fee_ym[{_inputFeeYm}] s_input_hosp_id[{_sInputHospId}] sqlcode[0] rec[{rowsAffected}]");
                    }
                }

                _oraConn.Commit();

                _iLoopCount = 0;

                // 由 (icei_cht_Fee) 中華電信方案院所繳費明細檔，取得要處理的醫事機構
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("SELECT DISTINCT HOSP_ID");
                    strSQL.AppendLine("  FROM ICEI_CHT_FEE A");
                    strSQL.AppendLine(" WHERE TO_DATE(:inputFeeYm,'YYYYMMDD')");
                    strSQL.AppendLine("       BETWEEN TO_DATE(TO_CHAR(A.FEE_S_DATE,'YYYYMM')||'01','YYYYMMDD')");
                    strSQL.AppendLine("           AND TO_DATE(TO_CHAR(A.FEE_E_DATE,'YYYYMM')||'01','YYYYMMDD')");
                    strSQL.AppendLine("   AND (");
                    strSQL.AppendLine("         HOSP_ID = DECODE(UPPER(:sInputHospId),'ALL',HOSP_ID,:sInputHospId)");
                    strSQL.AppendLine("         OR (:sInputHospId IS NULL AND PAY_CEILING IS NULL)");
                    strSQL.AppendLine("        )");
                    strSQL.AppendLine(" ORDER BY HOSP_ID DESC");
                    cmd.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                    cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));

                    cmd.CommandText = strSQL.ToString();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _sHospId = reader.GetString(0);
                            _iTotMonthlyFee = 0;
                            _iTotPaymentFee = 0;
                            _iFPayAmt = 0;
                            _iCPayAmt = 0;

                            Console.WriteLine("\n\n");
                            Console.WriteLine($" =========== HOSP_ID :[{_sHospId}] =========== \n");
                            Console.WriteLine("                                   轉入月租費  可補付金額  實際核定月租費   已暫付金額(C) 補付金額(F) 計算支付上限註記 ");
                            Console.WriteLine(" FEE_S_DATE FEE_E_DATE APPL_RATE  PAYMENT_FEE     CEILING     MONTHLY_FEE      PAY_AMT(C)  PAY_AMT(F)                  ");
                            Console.WriteLine(" ---------- ---------- --------- ------------  ----------  --------------   ------------- ----------- ---------------- ");

                            // 查詢支付上限
                            using (OracleCommand cmdPayCeiling = _oraConn.CreateCommand())
                            {
                                StringBuilder sqlPayCeiling = new StringBuilder();
                                sqlPayCeiling.AppendLine("SELECT PAY_CEILING");
                                sqlPayCeiling.AppendLine("  FROM ICEI_AUDIT_DTL");
                                sqlPayCeiling.AppendLine(" WHERE DATA_TYPE = 'E'");
                                sqlPayCeiling.AppendLine("   AND AUDIT_STATUS IN ('5','D','F')");
                                sqlPayCeiling.AppendLine("   AND HOSP_ID = :sHospId");
                                sqlPayCeiling.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                                sqlPayCeiling.AppendLine("   AND ROWNUM = 1");
                                cmdPayCeiling.Parameters.Add(new OracleParameter("sHospId", _sHospId));
                                cmdPayCeiling.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));

                                cmdPayCeiling.CommandText = sqlPayCeiling.ToString();
                                object result = cmdPayCeiling.ExecuteScalar();

                                if (result != null && result != DBNull.Value)
                                {
                                    _iPayCeiling = Convert.ToInt32(result);
                                }
                                else
                                {
                                    // 抓 ICEI_CHT_APPL_LIST (鼓勵醫療院所即時查詢病患就醫資訊方案申請者名單)
                                    StringBuilder sqlApplList = new StringBuilder();
                                    sqlApplList.AppendLine("SELECT PAY_CEILING");
                                    sqlApplList.AppendLine("  FROM (");
                                    sqlApplList.AppendLine("        SELECT PAY_CEILING");
                                    sqlApplList.AppendLine("          FROM ICEI_CHT_APPL_LIST");
                                    sqlApplList.AppendLine("         WHERE TO_DATE(:inputFeeYm,'YYYYMMDD')");
                                    sqlApplList.AppendLine("               BETWEEN TO_DATE(TO_CHAR(APPL_START_DATE,'YYYYMM')||'01','YYYYMMDD')");
                                    sqlApplList.AppendLine("                   AND TO_DATE(TO_CHAR(APPL_END_DATE,'YYYYMM')||'01','YYYYMMDD')");
                                    sqlApplList.AppendLine("           AND HOSP_ID = :sHospId");
                                    sqlApplList.AppendLine("           AND CHECK_MARK IN ('W','Y')");
                                    sqlApplList.AppendLine("         ORDER BY APPL_START_DATE DESC");
                                    sqlApplList.AppendLine("      )");
                                    sqlApplList.AppendLine(" WHERE ROWNUM = 1");
                                    cmdPayCeiling.Parameters.Clear();
                                    cmdPayCeiling.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                                    cmdPayCeiling.Parameters.Add(new OracleParameter("sHospId", _sHospId));

                                    cmdPayCeiling.CommandText = sqlApplList.ToString();
                                    result = cmdPayCeiling.ExecuteScalar();

                                    if (result != null && result != DBNull.Value)
                                    {
                                        _iPayCeiling = Convert.ToInt32(result);
                                    }

                                    if (_iMsg == 1)
                                    {
                                        Console.WriteLine($"    (支付上限)ICEI_CHT_APPL_LIST.PAY_CEILING :[{_iPayCeiling}]");
                                        Console.WriteLine($"        input_fee_ym    [{_inputFeeYm}]");
                                        Console.WriteLine($"        s_hosp_id       [{_sHospId}]");
                                        Console.WriteLine($"        i_pay_ceiling_2 [{_iPayCeiling2}]");
                                    }
                                }
                            }

                            _iPayCeiling2 = _iPayCeiling;

                            // 查詢已暫付金額
                            using (OracleCommand cmdPayAmt = _oraConn.CreateCommand())
                            {
                                StringBuilder sqlPayAmt = new StringBuilder();
                                sqlPayAmt.AppendLine("SELECT SUM(PAY_AMT)");
                                sqlPayAmt.AppendLine("  FROM ICEI_AUDIT_DTL");
                                sqlPayAmt.AppendLine(" WHERE DATA_TYPE IN ('C','F')");
                                sqlPayAmt.AppendLine("   AND HOSP_ID = :sHospId");
                                sqlPayAmt.AppendLine("   AND AUDIT_STATUS IN ('5','D','F')");
                                sqlPayAmt.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                                cmdPayAmt.Parameters.Add(new OracleParameter("sHospId", _sHospId));
                                cmdPayAmt.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));

                                cmdPayAmt.CommandText = sqlPayAmt.ToString();
                                object result = cmdPayAmt.ExecuteScalar();

                                if (result != null && result != DBNull.Value)
                                {
                                    _iCPayAmt = Convert.ToInt32(result);
                                }

                                if (_iMsg == 1)
                                {
                                    Console.WriteLine("    select sum(pay_amt) icei_audit_dtl data_type = 'C'");
                                    Console.WriteLine($"           i_C_pay_amt    :[{_iCPayAmt}]");
                                    Console.WriteLine($"           s_hosp_id      :[{_sHospId}]");
                                    Console.WriteLine($"           input_fee_ym   :[{_inputFeeYm}]");
                                }
                            }

                            // 由 (icei_cht_Fee) 中華電信方案院所繳費明細檔，PAY_CEILING
                            using (OracleCommand cmdChtFee = _oraConn.CreateCommand())
                            {
                                StringBuilder sqlChtFee = new StringBuilder();
                                sqlChtFee.AppendLine("SELECT ROWID,");
                                sqlChtFee.AppendLine("       HOSP_ID,");
                                sqlChtFee.AppendLine("       TO_CHAR(FEE_S_DATE,'YYYYMMDD') FEE_S_DATE,");
                                sqlChtFee.AppendLine("       TO_CHAR(FEE_E_DATE,'YYYYMMDD') FEE_E_DATE,");
                                sqlChtFee.AppendLine("       APPL_RATE,");
                                sqlChtFee.AppendLine("       MONTHLY_FEE,");
                                sqlChtFee.AppendLine("       PAYMENT_FEE,");
                                sqlChtFee.AppendLine("       PAY_CEILING,");
                                sqlChtFee.AppendLine("       PAY_CEILING_MARK");
                                sqlChtFee.AppendLine("  FROM ICEI_CHT_FEE A");
                                sqlChtFee.AppendLine(" WHERE TO_DATE(:inputFeeYm,'YYYYMMDD')");
                                sqlChtFee.AppendLine("       BETWEEN TO_DATE(TO_CHAR(A.FEE_S_DATE,'YYYYMM')||'01','YYYYMMDD')");
                                sqlChtFee.AppendLine("           AND TO_DATE(TO_CHAR(A.FEE_E_DATE,'YYYYMM')||'01','YYYYMMDD')");
                                sqlChtFee.AppendLine("   AND HOSP_ID = :sHospId");
                                sqlChtFee.AppendLine(" ORDER BY HOSP_ID DESC, PAY_CEILING_MARK ASC, FEE_S_DATE ASC, FEE_E_DATE ASC");
                                cmdChtFee.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));
                                cmdChtFee.Parameters.Add(new OracleParameter("sHospId", _sHospId));

                                cmdChtFee.CommandText = sqlChtFee.ToString();
                                using (OracleDataReader readerChtFee = cmdChtFee.ExecuteReader())
                                {
                                    while (readerChtFee.Read())
                                    {
                                        SQL100 sql100 = new SQL100
                                        {
                                            rowid = readerChtFee.GetString(0),
                                            hospId = readerChtFee.GetString(1),
                                            feeSDate = readerChtFee.GetString(2),
                                            feeEDate = readerChtFee.GetString(3),
                                            applRate = readerChtFee.GetString(4),
                                            monthlyFee = readerChtFee.GetInt32(5),
                                            paymentFee = readerChtFee.GetInt32(6),
                                            payCeiling = readerChtFee.IsDBNull(7) ? 0 : readerChtFee.GetInt32(7),
                                            payCeilingMark = readerChtFee.IsDBNull(8) ? string.Empty : readerChtFee.GetString(8)
                                        };

                                        _iPayCeiling1 = _iPayCeiling2;

                                        if (sql100.paymentFee <= _iPayCeiling2)
                                        {
                                            _iMonthlyFee = sql100.paymentFee;
                                            _iPayCeiling2 = _iPayCeiling2 - sql100.paymentFee;
                                        }
                                        else
                                        {
                                            _iMonthlyFee = _iPayCeiling2;
                                            _iPayCeiling2 = 0;
                                        }

                                        if (sql100.payCeilingMark == "A")
                                        {
                                            Console.WriteLine($"   {sql100.feeSDate} {sql100.feeEDate}     {sql100.applRate}     {sql100.paymentFee,5}       {_iPayCeiling1,5}           {_iMonthlyFee,5}           {_iCPayAmt,5}              A - icei1460b01");

                                            if (_iCPayAmt != _iMonthlyFee)
                                            {
                                                _oraConn.RollbackTransaction();

                                                Console.WriteLine(" 已暫付金額(C)i_C_pay_amt 與 實際核定月租費 i_monthly_fee 不符，請查明原因。");
                                                Console.WriteLine($" i_C_pay_amt  :[{_iCPayAmt}]");
                                                Console.WriteLine($" i_monthly_fee:[{_iMonthlyFee}]");

                                                break;
                                            }
                                        }
                                        else
                                        {
                                            // 更新 icei_cht_fee
                                            using (OracleCommand cmdUpdate = _oraConn.CreateCommand())
                                            {
                                                StringBuilder sqlUpdate = new StringBuilder();
                                                sqlUpdate.AppendLine("UPDATE ICEI_CHT_FEE");
                                                sqlUpdate.AppendLine("   SET MONTHLY_FEE = :iMonthlyFee,");
                                                sqlUpdate.AppendLine("       PAY_CEILING = :iPayCeiling,");
                                                sqlUpdate.AppendLine("       PAY_CEILING_MARK = 'Y'");
                                                sqlUpdate.AppendLine(" WHERE ROWID = :rowid");
                                                cmdUpdate.Parameters.Add(new OracleParameter("iMonthlyFee", _iMonthlyFee));
                                                cmdUpdate.Parameters.Add(new OracleParameter("iPayCeiling", _iPayCeiling));
                                                cmdUpdate.Parameters.Add(new OracleParameter("rowid", sql100.rowid));

                                                cmdUpdate.CommandText = sqlUpdate.ToString();
                                                cmdUpdate.ExecuteNonQuery();

                                                if (_iMsg == 1)
                                                {
                                                    Console.WriteLine($"    update icei_cht_fee i_pay_ceiling :[{_iPayCeiling}]");
                                                    Console.WriteLine($"           rowid[{sql100.rowid}]");
                                                }
                                            }

                                            Console.WriteLine($"   {sql100.feeSDate} {sql100.feeEDate}     {sql100.applRate}     {sql100.paymentFee,5}       {_iPayCeiling1,5}           {_iMonthlyFee,5}                       {_iMonthlyFee,5}  Y - icei2028b01");

                                            // 插入 icei_audit_dtl
                                            using (OracleCommand cmdInsert = _oraConn.CreateCommand())
                                            {
                                                StringBuilder sqlInsert = new StringBuilder();
                                                sqlInsert.AppendLine("INSERT INTO ICEI_AUDIT_DTL (");
                                                sqlInsert.AppendLine("       BRANCH_CODE, DATA_TYPE, HOSP_ID");
                                                sqlInsert.AppendLine("       ,AUDIT_STATUS, PAY_AMT, CHT_FEE");
                                                sqlInsert.AppendLine("       ,SEASON_TOT, FEE_YM, HOSP_CNT_TYPE, FEE_QQ, SEASON_CNT");
                                                sqlInsert.AppendLine("       ,SEASON_S_DATE, SEASON_E_DATE, PLAN_S_DATE, PLAN_E_DATE");
                                                sqlInsert.AppendLine("       ,AUDIT_HP_DATE, AUDIT_HP_CNT, AUDIT_HP_QRY, AUDIT_HP_RATE, AUDIT_HP_W");
                                                sqlInsert.AppendLine("       ,AUDIT_HP_IDX, AUDIT_OP_DATE, AUDIT_OP_CNT, AUDIT_OP_QRY, AUDIT_OP_RATE");
                                                sqlInsert.AppendLine("       ,AUDIT_OP_W, AUDIT_OP_IDX, AUDIT_CARE_DATE, AUDIT_CARE_CNT, AUDIT_CARE_QRY");
                                                sqlInsert.AppendLine("       ,AUDIT_CARE_RATE, AUDIT_CARE_W, AUDIT_CARE_IDX, AUDIT_CHK_DATE, AUDIT_CHK_APRV_OK");
                                                sqlInsert.AppendLine("       ,AUDIT_CHK_APRV_DATE, AUDIT_CHK_W, AUDIT_SPEC_DATE, AUDIT_SPEC_CNT, AUDIT_SPEC_QRY");
                                                sqlInsert.AppendLine("       ,AUDIT_SPEC_RATE, AUDIT_SPEC_W, AUDIT_SPEC_IDX, CREATE_DATE");
                                                sqlInsert.AppendLine("       ,TXT_DATE, UPLOAD_RATE1, UPLOAD_RATE2, PRSNID_RATE, OPFEE_RATE");
                                                sqlInsert.AppendLine("       ,OPFEE_P_RATE, ORDER_RATE, PRICODE_RATE, HOSP_DATA_TYPE");
                                                sqlInsert.AppendLine("       ,APPL_TYPE, APPL_DATE, ACPT_DATE, OIPD_TYPE, AUDIT_ORDER_CNT");
                                                sqlInsert.AppendLine("       ,AUDIT_ORDER_QRY, AUDIT_ORDER_RATE, AUDIT_ORDER_W, AUDIT_ORDER_IDX, AUDIT_ORDER_DATE");
                                                sqlInsert.AppendLine("       ,AUDIT_KEEN_DATE, AUDIT_KEEN_CNT, AUDIT_KEEN_QRY, AUDIT_KEEN_RATE, AUDIT_KEEN_W");
                                                sqlInsert.AppendLine("       ,AUDIT_KEEN_IDX, AUDIT_EM_DATE, AUDIT_EM_CNT, AUDIT_EM_QRY, AUDIT_EM_RATE");
                                                sqlInsert.AppendLine("       ,AUDIT_EM_W, AUDIT_EM_IDX, AUDIT_CTMRI_DATE, AUDIT_CTMRI_CNT, AUDIT_CTMRI_QRY");
                                                sqlInsert.AppendLine("       ,AUDIT_CTMRI_RATE, AUDIT_CTMRI_IDX, CTMRI_FEE_MARK, AUDIT_CTMRI_REACH_MARK, PREAPV_AMT");
                                                sqlInsert.AppendLine("       ,PAY_CEILING)");
                                                sqlInsert.AppendLine("SELECT BRANCH_CODE");
                                                sqlInsert.AppendLine("       ,'F' DATA_TYPE");
                                                sqlInsert.AppendLine("       ,HOSP_ID");
                                                sqlInsert.AppendLine("       ,'5' AUDIT_STATUS");
                                                sqlInsert.AppendLine("       ,:iMonthlyFee PAY_AMT");
                                                sqlInsert.AppendLine("       ,:paymentFee CHT_FEE");
                                                sqlInsert.AppendLine("       ,SEASON_TOT, FEE_YM, HOSP_CNT_TYPE, FEE_QQ, SEASON_CNT");
                                                sqlInsert.AppendLine("       ,TO_DATE(:feeSDate,'YYYYMMDD') SEASON_S_DATE");
                                                sqlInsert.AppendLine("       ,TO_DATE(:feeEDate,'YYYYMMDD') SEASON_E_DATE");
                                                sqlInsert.AppendLine("       ,PLAN_S_DATE, PLAN_E_DATE");
                                                sqlInsert.AppendLine("       ,AUDIT_HP_DATE, AUDIT_HP_CNT, AUDIT_HP_QRY, AUDIT_HP_RATE, AUDIT_HP_W");
                                                sqlInsert.AppendLine("       ,AUDIT_HP_IDX, AUDIT_OP_DATE, AUDIT_OP_CNT, AUDIT_OP_QRY, AUDIT_OP_RATE");
                                                sqlInsert.AppendLine("       ,AUDIT_OP_W, AUDIT_OP_IDX, AUDIT_CARE_DATE, AUDIT_CARE_CNT, AUDIT_CARE_QRY");
                                                sqlInsert.AppendLine("       ,AUDIT_CARE_RATE, AUDIT_CARE_W, AUDIT_CARE_IDX, AUDIT_CHK_DATE, AUDIT_CHK_APRV_OK");
                                                sqlInsert.AppendLine("       ,AUDIT_CHK_APRV_DATE, AUDIT_CHK_W, AUDIT_SPEC_DATE, AUDIT_SPEC_CNT, AUDIT_SPEC_QRY");
                                                sqlInsert.AppendLine("       ,AUDIT_SPEC_RATE, AUDIT_SPEC_W, AUDIT_SPEC_IDX");
                                                sqlInsert.AppendLine("       ,SYSDATE CREATE_DATE, SYSDATE TXT_DATE, UPLOAD_RATE1, UPLOAD_RATE2, PRSNID_RATE, OPFEE_RATE");
                                                sqlInsert.AppendLine("       ,OPFEE_P_RATE, ORDER_RATE, PRICODE_RATE");
                                                sqlInsert.AppendLine("       ,HOSP_DATA_TYPE, APPL_TYPE, APPL_DATE, ACPT_DATE, OIPD_TYPE, AUDIT_ORDER_CNT");
                                                sqlInsert.AppendLine("       ,AUDIT_ORDER_QRY, AUDIT_ORDER_RATE, AUDIT_ORDER_W, AUDIT_ORDER_IDX, AUDIT_ORDER_DATE");
                                                sqlInsert.AppendLine("       ,AUDIT_KEEN_DATE, AUDIT_KEEN_CNT, AUDIT_KEEN_QRY, AUDIT_KEEN_RATE, AUDIT_KEEN_W");
                                                sqlInsert.AppendLine("       ,AUDIT_KEEN_IDX, AUDIT_EM_DATE, AUDIT_EM_CNT, AUDIT_EM_QRY, AUDIT_EM_RATE");
                                                sqlInsert.AppendLine("       ,AUDIT_EM_W, AUDIT_EM_IDX, AUDIT_CTMRI_DATE, AUDIT_CTMRI_CNT, AUDIT_CTMRI_QRY");
                                                sqlInsert.AppendLine("       ,AUDIT_CTMRI_RATE, AUDIT_CTMRI_IDX, CTMRI_FEE_MARK, AUDIT_CTMRI_REACH_MARK, PREAPV_AMT");
                                                sqlInsert.AppendLine("       ,PAY_CEILING");
                                                sqlInsert.AppendLine("  FROM ICEI_AUDIT_DTL");
                                                sqlInsert.AppendLine(" WHERE HOSP_ID = :sInputHospId");
                                                sqlInsert.AppendLine("   AND FEE_YM = TO_DATE(:inputFeeYm,'YYYYMMDD')");
                                                sqlInsert.AppendLine("   AND DATA_TYPE = 'E'");
                                                sqlInsert.AppendLine("   AND AUDIT_STATUS IN ('5','D','F')");
                                                sqlInsert.AppendLine("   AND ROWNUM = 1");
                                                cmdInsert.Parameters.Add(new OracleParameter("iMonthlyFee", _iMonthlyFee));
                                                cmdInsert.Parameters.Add(new OracleParameter("paymentFee", sql100.paymentFee));
                                                cmdInsert.Parameters.Add(new OracleParameter("feeSDate", sql100.feeSDate));
                                                cmdInsert.Parameters.Add(new OracleParameter("feeEDate", sql100.feeEDate));
                                                cmdInsert.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                                                cmdInsert.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));

                                                cmdInsert.CommandText = sqlInsert.ToString();
                                                cmdInsert.ExecuteNonQuery();

                                                if (_iMsg == 1)
                                                {
                                                    Console.WriteLine("    INSERT INTO ICEI_AUDIT_DTL data_type = 'F'");
                                                    Console.WriteLine($"        i_monthly_fee     [{_iMonthlyFee}]");
                                                    Console.WriteLine($"        i_tot_payment_fee [{_iTotPaymentFee}]");
                                                    Console.WriteLine($"        s_input_hosp_id   [{_sInputHospId}]");
                                                    Console.WriteLine($"        input_fee_ym      [{_inputFeeYm}]");
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // 計算已支付金額
                            using (OracleCommand cmdTotal = _oraConn.CreateCommand())
                            {
                                StringBuilder sqlTotal = new StringBuilder();
                                sqlTotal.AppendLine("SELECT SUM(MONTHLY_FEE),");
                                sqlTotal.AppendLine("       SUM(PAYMENT_FEE)");
                                sqlTotal.AppendLine("  FROM ICEI_CHT_FEE A");
                                sqlTotal.AppendLine(" WHERE HOSP_ID = :sHospId");
                                sqlTotal.AppendLine("   AND TO_DATE(:inputFeeYm,'YYYYMMDD')");
                                sqlTotal.AppendLine("       BETWEEN TO_DATE(TO_CHAR(A.FEE_S_DATE,'YYYYMM')||'01','YYYYMMDD')");
                                sqlTotal.AppendLine("           AND TO_DATE(TO_CHAR(A.FEE_E_DATE,'YYYYMM')||'01','YYYYMMDD')");
                                sqlTotal.AppendLine(" ORDER BY HOSP_ID, TXT_DATE");
                                cmdTotal.Parameters.Add(new OracleParameter("sHospId", _sHospId));
                                cmdTotal.Parameters.Add(new OracleParameter("inputFeeYm", _inputFeeYm));

                                cmdTotal.CommandText = sqlTotal.ToString();
                                using (OracleDataReader readerTotal = cmdTotal.ExecuteReader())
                                {
                                    if (readerTotal.Read())
                                    {
                                        _iTotMonthlyFee = readerTotal.IsDBNull(0) ? 0 : readerTotal.GetInt32(0);
                                        _iTotPaymentFee = readerTotal.IsDBNull(1) ? 0 : readerTotal.GetInt32(1);
                                    }
                                }

                                if (_iTotMonthlyFee >= _iCPayAmt)
                                {
                                    _iFTotPayAmt = _iTotMonthlyFee - _iCPayAmt;
                                }
                                else
                                {
                                    _iFTotPayAmt = 0;
                                }

                                Console.WriteLine(" ---------- ---------- --------- ------------  ----------  --------------   ------------- ----------- ---------------- ");
                                Console.WriteLine($"                            小計        {_iTotPaymentFee,5}       {_iPayCeiling2,5}           {_iTotMonthlyFee,5}           {_iCPayAmt,5}       {_iFTotPayAmt,5}");

                                if (_iMsg == 1)
                                {
                                    Console.WriteLine("    select icei_cht_fee");
                                    Console.WriteLine($"        s_hosp_id         :[{_sHospId}]");
                                    Console.WriteLine($"        input_fee_ym      :[{_inputFeeYm}]");
                                    Console.WriteLine($"        i_tot_monthly_fee :[{_iTotMonthlyFee}]");
                                    Console.WriteLine($"        i_tot_payment_fee :[{_iTotPaymentFee}]");
                                    Console.WriteLine($"        i_C_pay_amt       :[{_iCPayAmt}]");
                                    Console.WriteLine($"        i_F_tot_pay_amt   :[{_iFTotPayAmt}]");
                                }
                            }

                            _oraConn.Commit();
                        }
                    }
                }

                _msg = $"\n程式 {AppDomain.CurrentDomain.FriendlyName} 結束\n";
                Console.WriteLine(_msg);
                _proList.exitCode = 0;
                _proList.message = _msg;
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
                // PXX_exit_process
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        /* ---------- parameter check ---------- */
        private static void CheckArg(string[] args)
        {
            // Original: check_arg()
            if (args.Length >= 1)
            {
                _inputFeeYm = args[0];
            }

            if (args.Length >= 2)
            {
                _sInputHospId = args[1];
            }

            if (args.Length == 0 || args.Length > 2)
            {
                _rtnCode = 1;
                string prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine("參數種類： 程式代號    暫付月   醫事機構代碼(選項)");
                Console.WriteLine("參數1   ： icei2028b01 FEE_YM               *未給醫事機構代碼，只做尚未計算給付上限的院所。");
                Console.WriteLine("範例1   ： icei2028b01 20190101              ");
                Console.WriteLine();
                Console.WriteLine("參數2   ： icei2028b01 FEE_YM   [HOSP_ID]   *提供醫事機構代碼，只做該院所。");
                Console.WriteLine("範例2   ： icei2028b01 20190101 3501120157    ");
                Console.WriteLine();
                Console.WriteLine("參數3   ： icei2028b01 FEE_YM   [ALL]       *參數給ALL，全部院所重新執行。");
                Console.WriteLine("範例3   ： icei2028b01 20190101  ALL        ");

                _proList.exitCode = 1;
                _proList.message = "參數個數不符";
                throw new ArgumentException(_proList.message);
            }

            Console.WriteLine($"input_fee_ym    :[{_inputFeeYm}]");
            Console.WriteLine($"s_input_hosp_id :[{_sInputHospId}]");

            _logger.Info($"Args → {string.Join(",", args)}");
        }

        #region SQL Error Handling
        private static void SqlError(OracleException ex)
        {
            // Original: sql_error()
            string errorMsg = $"{ex.Message}\nLast SQL: {ex.Source}\nLine: {ex.StackTrace}";
            Console.WriteLine(errorMsg);
            _logger.Error(errorMsg);
            MEDM_SysLib.MEDM_ExitProcess(200, errorMsg);
        }
        #endregion
    }
}
```