using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using JPMorrow.Tools.Revit.MEP.Selection;
using JPMorrow.Revit.ConduitRuns;
using JPMorrow.Tools.Diagnostics;
using JPMorrow.Revit.Documents;
using System.Windows.Forms;
using JPMorrow.Revit.Tools.Params;
using JPMorrow.Revit.Measurements;
using JPMorrow.Revit.Jbox;

namespace MainApp
{
	public struct IndexedSelection
	{
		public int idx;
		public Reference pick_reference;
	}

	[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.DB.Macros.AddInId("58F7B2B7-BF6D-4B39-BBF8-13F7D9AAE97E")]
	public partial class ThisApplication : IExternalCommand
	{
        public Result Execute(ExternalCommandData cData, ref string message, ElementSet elements)
        {
            var result = debugger.show_yesno(
				header:"JBox To Largest Conduit Diameter", 
				err:"This program will aid in tagging your " + 
				"selected junction boxes with thier largest attached diameter conduit.\n\n" +
				"After running this script, your junction boxes will have a new parameter on them called " +
				"'Largest Attached Diameter'. You will need to load an electrical fixture tag into the model" + 
				" and edit it to include that parameter. You may then tag your junction boxes with this tag to get the desired result.");

			if(result == DialogResult.No)
			{
                return Result.Succeeded;
            }

            string[] dataDirectories = new string[0];
            bool debugApp = false;

            // set revit documents
            ModelInfo revit_info = ModelInfo.StoreDocuments(cData, dataDirectories, debugApp);

            // load shared parameters
            DefinitionFile param_file = revit_info.DOC.Application.OpenSharedParameterFile();
			
			if(param_file == null)
			{
                debugger.show(
					header:"JBox To Largest Conduit Diameter", 
					err:"Shared parameter file not found. Please load " + 
					"a shared parameter file and rerun this program.");
                return Result.Succeeded;
            }
			
			// check if shared parameter file has the parameter
            bool generate_parameter = true;
			foreach(var g in param_file.Groups)
			{
				if(g.Name.Equals("Electrical Fixtures"))
				{
					foreach(var d in g.Definitions)
					{
						if(d.Name.Equals("Largest Attached Diameter"))
						{
                            generate_parameter = false;
                            break;
                        }
					}
                }
			}

            // check if the project parameters already has a parameter loaded by that name
            var current_bindings = revit_info.UIAPP.ActiveUIDocument.Document.ParameterBindings;
            var it = current_bindings.ForwardIterator();

            while(it.MoveNext())
			{
                var d = it.Key;
				if(d.Name.Equals("Largest Attached Diameter") && d.ParameterType == ParameterType.Length)
				{
                    generate_parameter = false;
                    break;
                }
            }

            if(generate_parameter)
			{
                using Transaction tx = new Transaction(revit_info.DOC, "Making Shared Parameter");
                tx.Start();
                var def_opts = new ExternalDefinitionCreationOptions("Largest Attached Diameter", ParameterType.Length);
                def_opts.Visible = true;
                var grp = param_file.Groups.Create("Electrical Fixtures");
                var def = grp.Definitions.Create(def_opts) as ExternalDefinition;

                // get categories
                Category cat = revit_info.DOC.Settings.Categories.get_Item(BuiltInCategory.OST_ElectricalFixtures);
                CategorySet set = revit_info.DOC.Application.Create.NewCategorySet();
                set.Insert(cat);

                // make instance binding (can also do type binding here)
                var binding = revit_info.DOC.Application.Create.NewInstanceBinding(set);
                revit_info.UIAPP.ActiveUIDocument.Document.ParameterBindings.Insert(def, binding);
                tx.Commit();
            }

            // start processing junction boxes
            var ids = revit_info.SEL.GetElementIds();
            var jbis = JboxInfo.ParseId(revit_info.DOC, ids, out var f);

            // get largest diameter for tagging
            using TransactionGroup tgrp = new TransactionGroup(revit_info.DOC, "Jbox Parameters");
            tgrp.Start();
            foreach(var jbi in jbis)
            {
				var ld = jbi.Connections.GetLargestDiameter(revit_info.DOC, out var lc);
				var len = RMeasure.LengthFromDbl(revit_info.DOC, ld);

                // check the parameter 'Largest Attached Diameter'
                ElementParamCheck pck = new ElementParamCheck(revit_info.DOC, jbi.JboxId, "Largest Attached Diameter");
                var lad = pck.Get<double>("Largest Attached Diameter");
				
				if(!lad.IsLoaded)
				{
                    f.Add(new JboxError(jbi.JboxId, "'Largest Attached Diameter' parameter is not loaded"));
                    continue;
				}
				
				// set parameter in Revit
				using Transaction tx = new Transaction(revit_info.DOC, "Setting Parameters");
                tx.Start();
                var box = revit_info.DOC.GetElement(jbi.JboxId);
                pck.Set<double>(box, "Largest Attached Diameter", ld);
                tx.Commit();
            }
            tgrp.Assimilate();

			if(f.Any())
			{
                debugger.show(
					header:"Jbox Tag Largest Diameter - Failed Jboxes", 
					err:JboxError.FormatErrors(f));
            }

            return Result.Succeeded;
        }

		#region startup
		private void Module_Startup(object sender, EventArgs e)
		{

		}

		private void Module_Shutdown(object sender, EventArgs e)
		{

		}
		#endregion

		#region Revit Macros generated code
		private void InternalStartup()
		{
			this.Startup += new System.EventHandler(Module_Startup);
			this.Shutdown += new System.EventHandler(Module_Shutdown);
		}
		#endregion
	}
}