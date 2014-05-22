using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Collections;
using System.Collections.Generic;
using DotNetNuke.Common.Utilities;
using NEvoWeb.Modules.NB_Store;

using DotNetNuke.Services.Localization;
using Satrabel.HttpModules.Provider;
using System.Text.RegularExpressions;
using DotNetNuke.Entities.Modules;

namespace Satrabel.OpenUrlRewriter.NbStore
{
    public class NbStoreUrlRuleProvider : UrlRuleProvider
    {
        public NbStoreUrlRuleProvider() { }

        public override List<UrlRule> GetRules(int PortalId)
        {
            List<UrlRule> Rules = new List<UrlRule>();
            Dictionary<string, Locale> dicLocales = LocaleController.Instance.GetLocales(PortalId);            
            ProductController pc = new ProductController();
            CategoryController cc = new CategoryController();
            ModuleController mc = new ModuleController();
            ArrayList modules = mc.GetModulesByDefinition(PortalId, "NB_Store_ProductList");            
            foreach (ModuleInfo module in modules.OfType<ModuleInfo>().Where(m=> m.IsDeleted == false))
            {
                Hashtable modSettings = mc.GetModuleSettings(module.ModuleID);
                
                int DetailTabId = Null.NullInteger;
                if (modSettings["lstProductTabs"] != null)
                {
                    string lstProductTabs = modSettings["lstProductTabs"].ToString();
                    int.TryParse(lstProductTabs, out DetailTabId);
                }
                bool RewriteProducts = module.TabID == DetailTabId;

                bool BrowseCategory = Null.NullBoolean;
                string chkBrowseCategory = modSettings["chkBrowseCategory"] == null ? "" :  modSettings["chkBrowseCategory"].ToString();
                Boolean.TryParse(chkBrowseCategory, out BrowseCategory);

                bool IndexProducts = true;
                if (modSettings["chkIndexProducts"] != null)
                {
                    Boolean.TryParse(modSettings["chkIndexProducts"].ToString(), out IndexProducts);
                }


                int CatID = Null.NullInteger;
                string ddlDefaultCategory = modSettings["ddlDefaultCategory"] == null ? "" : modSettings["ddlDefaultCategory"].ToString();
                int.TryParse(ddlDefaultCategory, out CatID);
                foreach (KeyValuePair<string, Locale> key in dicLocales)
                {
                    string CultureCode = key.Value.Code;
                    string RuleCultureCode = (dicLocales.Count > 1 ? CultureCode : null);
                    bool chkCascadeResults = false;
                    bool.TryParse(modSettings["chkCascadeResults"] == null ? "" : modSettings["chkCascadeResults"].ToString(), out chkCascadeResults);

                    if (RewriteProducts)
                    {
                        var prodLst = GetProductList(PortalId, pc, cc, CatID, CultureCode, chkCascadeResults);

                        //var prodLst = pc.GetProductList(PortalId, CatID, CultureCode, false);
                        foreach (ProductListInfo prod in prodLst)
                        {
                            var rule = new UrlRule
                            {
                                RuleType = UrlRuleType.Module,
                                CultureCode = RuleCultureCode,
                                TabId = module.TabID,
                                Parameters = "ProdID=" + prod.ProductID + (CatID == Null.NullInteger ? "" : "&" + "CatID=" + CatID),
                                Action = UrlRuleAction.Rewrite,
                                Url = CleanupUrl(prod.SEOName == "" ? prod.ProductName : prod.SEOName),
                                InSitemap = IndexProducts
                            };
                            Rules.Add(rule);
                        }
                    }
                    if (BrowseCategory) 
                    {
                        var CatRules = GetRulesForCategory(PortalId, CultureCode, module.TabID, CatID, "", pc, cc, RuleCultureCode, chkCascadeResults, RewriteProducts, IndexProducts);
                        Rules.AddRange(CatRules);
                    }
                }
            }
            return Rules;
        }

        private static ArrayList GetProductList(int PortalId, ProductController pc, CategoryController cc, int CatID, string CultureCode, bool chkCascadeResults)
        {
            ArrayList prodLst;
            string CategoryList = "";
            if (chkCascadeResults && CatID > 0)
            {
                var aryList = cc.GetCategories(PortalId, CultureCode);                
                CategoryList = cc.GetSubCategoryList(aryList, CatID);
                CategoryList += CatID.ToString();
                prodLst = pc.GetProductList(PortalId, CatID, CultureCode, "", false, false, "", false, 0, 1, 99999, false, false, CategoryList, false);
            }
            else {
                prodLst = pc.GetProductList(PortalId, CatID, CultureCode, false);
            }
            return prodLst;
        }

        private List<UrlRule> GetRulesForCategory(int PortalId, string CultureCode, int TabId, int CatID, string CategoryUrl, ProductController pc, CategoryController cc, string RuleCultureCode, bool chkCascadeResults, bool RewriteProducts, bool IndexProducts)
        {
            List<UrlRule> Rules = new List<UrlRule>();
            var catLst = cc.GetCategories(PortalId, CultureCode, CatID == Null.NullInteger ? 0 : CatID);
            foreach (NB_Store_CategoriesInfo cat in catLst)
            {
                var CatRule = new UrlRule
                {
                    RuleType = UrlRuleType.Module,
                    CultureCode = RuleCultureCode,
                    TabId = TabId,
                    Parameters = "CatID=" + cat.CategoryID,
                    Action = UrlRuleAction.Rewrite,
                    Url = (CategoryUrl == "" ? "" : CategoryUrl + "/") + CleanupUrl(cat.SEOName == "" ? cat.CategoryName : cat.SEOName),
                    InSitemap = IndexProducts
                };
                CatRule.RedirectDestination = CatRule.Parameters.Replace('=', '/').Replace('&', '/') + "/" + CleanupSEO(cat.SEOName == "" ? cat.CategoryName : cat.SEOName);
                CatRule.RedirectDestination = CatRule.RedirectDestination.ToLower();
                if (string.IsNullOrEmpty(CatRule.Url)) 
                {
                    //continue;
                }
                Rules.Add(CatRule);

                if (RewriteProducts)
                {
                    var productLst = GetProductList(PortalId, pc, cc, cat.CategoryID, CultureCode, chkCascadeResults);
                    //var productLst = pc.GetProductList(PortalId, cat.CategoryID, CultureCode, false);
                    foreach (ProductListInfo prod in productLst)
                    {
                        var rule = new UrlRule
                        {
                            RuleType = UrlRuleType.Module,
                            CultureCode = RuleCultureCode,
                            TabId = TabId,
                            Parameters = "ProdID=" + prod.ProductID + "&" + "CatID=" + cat.CategoryID,
                            Action = UrlRuleAction.Rewrite,
                            Url = (CategoryUrl == "" ? "" : CategoryUrl + "/") + CleanupUrl(cat.SEOName == "" ? cat.CategoryName : cat.SEOName) + "/" + CleanupUrl(prod.SEOName == "" ? prod.ProductName : prod.SEOName),
                            InSitemap = IndexProducts
                        };
                        Rules.Add(rule);
                    }
                }
                var CatRules = GetRulesForCategory(PortalId, CultureCode, TabId, cat.CategoryID, CatRule.Url, pc, cc, RuleCultureCode, chkCascadeResults, RewriteProducts, IndexProducts);
                Rules.AddRange(CatRules);
            }
            return Rules;
        }

        public List<UrlRule> GetRulesOld(int PortalId)
        {
            List<UrlRule> Rules = new List<UrlRule>();

            Dictionary<string, Locale> dicLocales = LocaleController.Instance.GetLocales(PortalId);
            foreach (KeyValuePair<string, Locale> key in dicLocales)
            {
                string CultureCode = key.Value.Code;
                CategoryController cc = new CategoryController();
                ProductController pc = new ProductController();
                // products alone
                var prodLst = pc.GetProductList(PortalId, Null.NullInteger, CultureCode, false);
                foreach (ProductListInfo prod in prodLst)
                {
                    var rule = new UrlRule
                    {
                        RuleType = UrlRuleType.Module,
                        CultureCode = (dicLocales.Count > 1 ? CultureCode : null),
                        Parameters = "ProdID=" + prod.ProductID ,
                        Action = UrlRuleAction.Rewrite,
                        Url = CleanupUrl(prod.SEOName == "" ? prod.ProductName : prod.SEOName)                        
                    };
                    Rules.Add(rule);
                }
                // products and categories
                var catLst = cc.GetCategories(PortalId, CultureCode);
                foreach (NB_Store_CategoriesInfo cat in catLst)
                {
                    var CatRule = new UrlRule
                    {
                        RuleType = UrlRuleType.Module,
                        CultureCode = (dicLocales.Count > 1 ? CultureCode : null),
                        Parameters = "CatID=" + cat.CategoryID,
                        Action = UrlRuleAction.Rewrite,
                        Url = CleanupUrl(cat.SEOName == "" ? cat.CategoryName : cat.SEOName)                        
                    };
                    CatRule.RedirectDestination = CatRule.Parameters.Replace('=', '/').Replace('&', '/') + "/" + CleanupSEO(cat.SEOName == "" ? cat.CategoryName : cat.SEOName);
                    CatRule.RedirectDestination = CatRule.RedirectDestination.ToLower();
                    Rules.Add(CatRule);

                    var productLst = pc.GetProductList(PortalId, cat.CategoryID, CultureCode, false);
                    foreach (ProductListInfo prod in productLst)
                    {
                        var rule = new UrlRule
                        {
                            RuleType = UrlRuleType.Module,
                            CultureCode = (dicLocales.Count > 1 ? CultureCode : null),
                            Parameters = "ProdID=" + prod.ProductID + "&" + "CatID=" + cat.CategoryID,
                            Action = UrlRuleAction.Rewrite,
                            Url = CleanupUrl(cat.SEOName == "" ? cat.CategoryName : cat.SEOName) +"/"+CleanupUrl(prod.SEOName == "" ? prod.ProductName : prod.SEOName)
                        };
                        Rules.Add(rule);
                    }
                }
            }
            return Rules;

        }

        private string CleanupSEO(string SEOName){
            if (SEOName != "") {
                SEOName = SEOName.Replace( " ", "_");
                SEOName = Regex.Replace(SEOName, @"[\W]", "");
            }
            return SEOName;
        }

                     
    }
}