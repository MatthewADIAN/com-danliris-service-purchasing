﻿using Com.DanLiris.Service.Purchasing.Lib.ViewModels.MonitoringCorrectionNoteExpenditureViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Com.DanLiris.Service.Purchasing.Lib.Interfaces
{
	public interface IMonitoringCorrectionNoteExpenditureFacade
	{
        Tuple<List<MonitoringCorrectionNoteExpenditureViewModel>, int> GetMonitoringKeluarNKReport(DateTime? dateFrom, DateTime? dateTo, int page, int size, string Order, int offset);
        MemoryStream GenerateExcelMonitoringKeluarNK(DateTime? dateFrom, DateTime? dateTo, int page, int size, string Order, int offset);

        Tuple<List<MonitoringCorrectionNoteExpenditureViewModel>, int> GetMonitoringKeluarNKByUserReport(DateTime? dateFrom, DateTime? dateTo, int page, int size, string Order, int offset);
        MemoryStream GenerateExcelMonitoringKeluarNKByUser(DateTime? dateFrom, DateTime? dateTo, int page, int size, string Order, int offset);
    }
}
