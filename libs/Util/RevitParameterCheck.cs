/*
    Author: Justin Morrow
    Date Created: 4/21/2021
    Date Updated: 4/22/2021
    Description: A Module that checked for the existance of parameters on a Revit element
*/

/*
    Example Usage {
        var sid = revit_info.SEL.GetElementIds().First();

        var jbox_params = new string[] { 
            "Nominal Diameter 1",
            "Nominal Diameter 2",
            "Nominal Diameter 3",
            "Nominal Diameter 4"
        };

        ElementParamCheck pck = new ElementParamCheck(revit_info.DOC, sid, jbox_params);
        var dbl_test = pck.Get<double>("Nominal Diameter 2");
        var str_test = RMeasure.LengthFromDbl(revit_info.DOC, dbl_test);
        debugger.show(err:str_test);
        return Result.Succeeded;
    }
*/

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using JPMorrow.Tools.Diagnostics;

namespace JPMorrow.Revit.Tools.Params
{
    public class ParameterResult<T>
    {
        public bool IsLoaded { get; private set; }
        public T Value { get; private set; }

        public ParameterResult(bool is_loaded, T value)
        {
            IsLoaded = is_loaded;
            Value = value;
        }
    }

    public class ElementParamCheck
    {
        private Document InternalDocument { get; set; }
        private List<ParamProp> ProccessedParams { get; set; } = new List<ParamProp>();

        public bool IsLoaded(string param_name)
        {
            var idx = ProccessedParams.FindIndex(x => x.Name.Equals(param_name));
            if(idx == -1) return false;
            return ProccessedParams[idx].IsLoaded;
        }

        /// <summary>
        /// Get a parameter value from the provided parameter name
        /// </summary>
        /// <returns>value if it matches, otherwise false</returns>
        public ParameterResult<T> Get<T>(string param_name)
        {
            var ttype = typeof(T);

            // find param
            var idx = ProccessedParams.FindIndex(x => x.Name.Equals(param_name));

            if(idx == -1)
            {
                debugger.show(
                    header:"Revit Parameter Check", 
                    err:"The parameter '" + param_name + "' does not exist in this ElementParameterCheck");
                return new ParameterResult<T>(false, default(T));
            }
                
            var param = ProccessedParams[idx];

            if(!param.IsLoaded)
            {
                debugger.show(
                    header:"Revit Parameter Check", 
                    err:"The parameter '" + param.Name + "' is not loaded");
                return new ParameterResult<T>(false, default(T));
            }

            if(param.ParamType != ttype)
            {
                debugger.show(
                    header:"Revit Parameter Check", 
                    err:"The parameter type does not match the requested template type");
                return new ParameterResult<T>(false, default(T));
            }

            return new ParameterResult<T>(true, (T)param.ParamValue);
        }

        public void Set<T>(Element element, string param_name, T val)
        {
            var t = typeof(T);
            if(t == typeof(int))
            {
                element.LookupParameter(param_name).Set((int)Convert.ChangeType(val, typeof(int)));
            }
            else if(t == typeof(double))
            {
                element.LookupParameter(param_name).Set((double)Convert.ChangeType(val, typeof(double)));
            }
            else if(t == typeof(string))
            {
                element.LookupParameter(param_name).Set((string)Convert.ChangeType(val, typeof(string)));
            }
            else if(t == typeof(ElementId))
            {
                element.LookupParameter(param_name).Set((ElementId)Convert.ChangeType(val, typeof(ElementId)));
            }
            else
            {
                debugger.show(
                    header:"Revit Parameter Check", 
                    err:"Value type is not valid for the setting of element parameters");
            }
        }

        public ElementParamCheck(Document doc, ElementId id, params string[] param_names)
        {
            InternalDocument = doc;
            var el = doc.GetElement(id);

            foreach(var n in param_names)
            {
                ProccessedParams.Add(new ParamProp(doc, el, n));
            }
        }

        /// <summary>
        /// Represents a parameter that has been proccessed for proper access
        /// </summary>
        private class ParamProp
        {
            public string Name { get; private set; }
            public Type ParamType { get; private set; }
            public object ParamValue { get; private set; }
            public bool IsLoaded { get; private set; }

            public ParamProp(Document doc, Element el, string param_name)
            {
                Parameter p = el.LookupParameter(param_name);
                Name = param_name;

                IsLoaded = true;
                ParamValue = null;
                ParamType = null;
                
                if(p == null) 
                {
                    IsLoaded = false;
                    return;
                }

                switch(p.StorageType)
                {
                    case StorageType.Integer:
                        ParamType = typeof(int);
                        ParamValue = p.AsInteger();
                        break;
                    case StorageType.String:
                        ParamType = typeof(string);
                        ParamValue = p.AsString();
                        break;
                    case StorageType.Double:
                        ParamType = typeof(double);
                        ParamValue = p.AsDouble();
                        break;
                    case StorageType.ElementId:
                        ParamType = typeof(ElementId);
                        ParamValue = p.AsElementId();
                        break;
                    default:
                        break;
                }
            }
        }
    }
}