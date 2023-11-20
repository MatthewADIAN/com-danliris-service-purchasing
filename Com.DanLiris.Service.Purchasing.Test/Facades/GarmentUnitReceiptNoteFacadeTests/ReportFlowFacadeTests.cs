﻿using AutoMapper;
using Com.DanLiris.Service.Purchasing.Lib;
using Com.DanLiris.Service.Purchasing.Lib.Facades.GarmentDeliveryOrderFacades;
using Com.DanLiris.Service.Purchasing.Lib.Facades.GarmentExternalPurchaseOrderFacades;
using Com.DanLiris.Service.Purchasing.Lib.Facades.GarmentInternalPurchaseOrderFacades;
using Com.DanLiris.Service.Purchasing.Lib.Facades.GarmentPurchaseRequestFacades;
using Com.DanLiris.Service.Purchasing.Lib.Facades.GarmentUnitDeliveryOrderFacades;
using Com.DanLiris.Service.Purchasing.Lib.Facades.GarmentUnitReceiptNoteFacades;
using Com.DanLiris.Service.Purchasing.Lib.Interfaces;
using Com.DanLiris.Service.Purchasing.Lib.Migrations;
using Com.DanLiris.Service.Purchasing.Lib.Models.GarmentDeliveryOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Services;
using Com.DanLiris.Service.Purchasing.Lib.ViewModels.GarmentUnitReceiptNoteViewModels;
using Com.DanLiris.Service.Purchasing.Lib.ViewModels.NewIntegrationViewModel;
using Com.DanLiris.Service.Purchasing.Test.DataUtils.GarmentDeliveryOrderDataUtils;
using Com.DanLiris.Service.Purchasing.Test.DataUtils.GarmentExternalPurchaseOrderDataUtils;
using Com.DanLiris.Service.Purchasing.Test.DataUtils.GarmentInternalPurchaseOrderDataUtils;
using Com.DanLiris.Service.Purchasing.Test.DataUtils.GarmentPurchaseRequestDataUtils;
using Com.DanLiris.Service.Purchasing.Test.DataUtils.GarmentUnitDeliveryOrderDataUtils;
using Com.DanLiris.Service.Purchasing.Test.DataUtils.GarmentUnitReceiptNoteDataUtils;
using Com.DanLiris.Service.Purchasing.Test.DataUtils.NewIntegrationDataUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Com.DanLiris.Service.Purchasing.Test.Facades.GarmentUnitReceiptNoteFacadeTests
{
	public class ReportFlowFacadeTests
	{ 
        private const string ENTITY = "GarmentUnitReceiptNoteReport";

		private const string USERNAME = "Unit Test";
		private IServiceProvider ServiceProvider { get; set; }

		[MethodImpl(MethodImplOptions.NoInlining)]
		public string GetCurrentMethod()
		{
			StackTrace st = new StackTrace();
			StackFrame sf = st.GetFrame(1);

			return string.Concat(sf.GetMethod().Name, "_", ENTITY);
		}

		private PurchasingDbContext _dbContext(string testName)
		{
			DbContextOptionsBuilder<PurchasingDbContext> optionsBuilder = new DbContextOptionsBuilder<PurchasingDbContext>();
			optionsBuilder
				.UseInMemoryDatabase(testName)
				.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));

			PurchasingDbContext dbContext = new PurchasingDbContext(optionsBuilder.Options);

			return dbContext;
		}

		private Mock<IServiceProvider> GetServiceProvider()
		{
			HttpResponseMessage message = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
			message.Content = new StringContent("{\"apiVersion\":\"1.0\",\"statusCode\":200,\"message\":\"Ok\",\"data\":[{\"Id\":7,\"codeRequirement\":\"BB\",\"code\":\"BB\",\"rate\":13700.0,\"name\":\"FABRIC\",\"date\":\"2018/10/20\"}],\"info\":{\"count\":1,\"page\":1,\"size\":1,\"total\":2,\"order\":{\"date\":\"desc\"},\"select\":[\"Id\",\"code\",\"rate\",\"date\"]}}");
			var HttpClientService = new Mock<IHttpClientService>();
			HttpClientService
				.Setup(x => x.GetAsync(It.IsAny<string>()))
				.ReturnsAsync(message);


			var serviceProvider = new Mock<IServiceProvider>();
			serviceProvider
				.Setup(x => x.GetService(typeof(IdentityService)))
				.Returns(new IdentityService() { Token = "Token", Username = "Test" });

			serviceProvider
				.Setup(x => x.GetService(typeof(IHttpClientService)))
				.Returns(HttpClientService.Object);

			

			return serviceProvider;
		}

		private GarmentUnitReceiptNoteDataUtil garmentUnitReceiptNoteDataUtil(GarmentUnitReceiptNoteFacade garmentUnitReceiptNoteFacade, string testName)
		{
			var garmentPurchaseRequestFacade = new GarmentPurchaseRequestFacade(GetServiceProvider().Object, _dbContext(testName));
			var garmentPurchaseRequestDataUtil = new GarmentPurchaseRequestDataUtil(garmentPurchaseRequestFacade);

			var garmentInternalPurchaseOrderFacade = new GarmentInternalPurchaseOrderFacade(_dbContext(testName), GetServiceProvider().Object);
			var garmentInternalPurchaseOrderDataUtil = new GarmentInternalPurchaseOrderDataUtil(garmentInternalPurchaseOrderFacade, garmentPurchaseRequestDataUtil);

			var garmentExternalPurchaseOrderFacade = new GarmentExternalPurchaseOrderFacade(GetServiceProvider().Object, _dbContext(testName));
			var garmentExternalPurchaseOrderDataUtil = new GarmentExternalPurchaseOrderDataUtil(garmentExternalPurchaseOrderFacade, garmentInternalPurchaseOrderDataUtil);

			var garmentDeliveryOrderFacade = new GarmentDeliveryOrderFacade(GetServiceProvider().Object, _dbContext(testName));
			var garmentDeliveryOrderDataUtil = new GarmentDeliveryOrderDataUtil(garmentDeliveryOrderFacade, garmentExternalPurchaseOrderDataUtil);

			return new GarmentUnitReceiptNoteDataUtil(garmentUnitReceiptNoteFacade, garmentDeliveryOrderDataUtil, null);
		}

		private GarmentUnitDeliveryOrderDataUtil UnitDOdataUtil(GarmentUnitDeliveryOrderFacade garmentUnitDeliveryOrderFacade, string testName)
		{
			var garmentUnitReceiptNoteFacade = new GarmentUnitReceiptNoteFacade(GetServiceProvider().Object, _dbContext(testName));
			var garmentUnitReceiptNoteDataUtil = this.garmentUnitReceiptNoteDataUtil(garmentUnitReceiptNoteFacade, testName);

			return new GarmentUnitDeliveryOrderDataUtil(garmentUnitDeliveryOrderFacade, garmentUnitReceiptNoteDataUtil);
		}
		[Fact]
		public async Task Should_Success_GENERATEEXCELSMP1()
		{
			var dbContext = _dbContext(GetCurrentMethod());
			var facade = new GarmentUnitReceiptNoteFacade(GetServiceProvider().Object, _dbContext(GetCurrentMethod()));
			var data = await garmentUnitReceiptNoteDataUtil(facade, GetCurrentMethod()).GetTestDataMonitoringFlow();
			var Response = facade.GenerateExcelFlowForUnit(DateTime.Now.AddDays(-7), DateTime.Now.AddDays(7), "SMP1", "BB", "fabric", 7, "SAMPLE");

			Assert.NotNull(Response);

		}
		[Fact]
		public async Task Should_Success_GENERATEEXCENotSample()
		{
			var dbContext = _dbContext(GetCurrentMethod());
			var facade = new GarmentUnitReceiptNoteFacade(GetServiceProvider().Object, _dbContext(GetCurrentMethod()));
			var data = await garmentUnitReceiptNoteDataUtil(facade, GetCurrentMethod()).GetTestDataMonitoringFlow();
			var Response = facade.GenerateExcelFlowForUnit(DateTime.Now.AddDays(-7), DateTime.Now.AddDays(7), "", "BB", "fabric", 7, "SAMPLE");

			Assert.NotNull(Response);

		}

		[Fact]
		public async Task Should_Success_Get_Monitoring()
		{
			var dbContext = _dbContext(GetCurrentMethod());
			var facade = new GarmentUnitReceiptNoteFacade(GetServiceProvider().Object, _dbContext(GetCurrentMethod()));
			var data = await garmentUnitReceiptNoteDataUtil(facade, GetCurrentMethod()).GetTestDataMonitoringFlow();
			var Response = facade.GetReportFlow(null, null,"","BB", 1, 25, "{}", 7);
			Assert.NotNull(Response.Item1);
		}
		[Fact]
		public async Task Should_Success_Get_MonitoringSMP()
		{
			var dbContext = _dbContext(GetCurrentMethod());
			var facade = new GarmentUnitReceiptNoteFacade(GetServiceProvider().Object, _dbContext(GetCurrentMethod()));
			var data = await garmentUnitReceiptNoteDataUtil(facade, GetCurrentMethod()).GetTestDataMonitoringFlow();
			var Response = facade.GetReportFlow(null, null, "SMP1", "BB", 1, 25, "{}", 7);
			Assert.NotNull(Response.Item1);
		}
	}
}
