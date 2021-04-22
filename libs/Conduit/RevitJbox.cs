/*
    Author: Justin Morrow
    Date Created: 4/21/2021
    Description: A module that handles junction box parsing with Revit
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Autodesk.Revit.DB;
using JPMorrow.Revit.RvtMiscUtil;
using JPMorrow.Revit.Tools.Params;

namespace JPMorrow.Revit.Jbox
{
    public class JboxError
    {
        public ElementId JboxId { get; private set; }
        public string ErrorMessage { get; private set; }

        public JboxError(ElementId jbox_id, string error_message)
        {
            JboxId = jbox_id;
            ErrorMessage = error_message;
        }

        public static string FormatErrors(IEnumerable<JboxError> errors)
        {
            return string.Join("\n", errors.Select(x => x.JboxId.IntegerValue.ToString() + " - " + x.ErrorMessage));
        }
    }

    [DataContract]
    public class JboxInfo
    {
        [DataMember]
        public JboxConnection Connections { get; private set; }
        [DataMember]
        public ElementId JboxId { get; private set; }

        private JboxInfo(ElementId id, JboxConnection connections) 
        {
            Connections = connections;
            JboxId = id;
        }

        public static IEnumerable<JboxInfo> ParseId(
            Document doc, IEnumerable<ElementId> box_ids, 
            out List<JboxError> failed_boxes)
        {
            failed_boxes = new List<JboxError>();
            var final_infos = new List<JboxInfo>();
            var ids = box_ids.ToList();

            foreach(var id in ids)
            {
                var jbox = doc.GetElement(id);

                if(!jbox.Category.Name.Equals("Electrical Fixtures"))
                {
                    failed_boxes.Add(new JboxError(jbox.Id, "Not an electrical fixture"));
                    continue;
                }

                JboxConnection connections = new JboxConnection(doc, jbox);

                if(connections.ConnectedConduitCount == 0)
                {
                    failed_boxes.Add(new JboxError(jbox.Id, "The jbox has no connected conduit"));
                    continue;
                }

                final_infos.Add(new JboxInfo(id, connections));
            }

            return final_infos;
        }
    }

    /// <summary>
    /// Get information about conduit that is connected to a junction box 
    /// </summary>
    public class JboxConnection
    {
        private List<ElementId> ConnectedConduit { get; set; } = new List<ElementId>();
        public int ConnectedConduitCount { get => ConnectedConduit.Count(); }
        private static double ConnectorTolerance { get; } = .00001;

        public JboxConnection(Document doc, Element jbox)
        {
            var cc = RvtUtil.GetNonSetConnectors(jbox, RvtUtil.GetConnectors);

            foreach(Connector c in cc)
            {
                if(!c.IsConnected) continue;

                // get connected conduit
                var all_refs = RvtUtil.GetConnectorListFromSet(c.AllRefs).ToList();
                all_refs.Remove(c);
                var idx = all_refs.FindIndex(x => IsConnectedTo(doc, c, x));
                if(idx == -1) continue;
                ConnectedConduit.Add(all_refs[idx].Owner.Id);
            }
        }

        private Dictionary<ElementId, double> GetAllDiameters(Document doc)
        {
            Dictionary<ElementId, double> diameters = new Dictionary<ElementId, double>();
            
            foreach(ElementId id in ConnectedConduit)
            {
                var el = doc.GetElement(id);
                var pck = new ElementParamCheck(doc, id, "Diameter(Trade Size)");
                var dia = pck.Get<double>("Diameter(Trade Size)");
                if(!dia.IsLoaded) continue;
                diameters.Add(id, dia.Value);
            }

            return diameters;
        }

        /// <summary>
        /// Gets the largest diameter of all the conduits connected to this junction box
        /// </summary>
        public double GetLargestDiameter(Document doc, out List<ElementId> largest_conduits)
        {
            largest_conduits = new List<ElementId>();
            var diameters = GetAllDiameters(doc);
            if(!diameters.Any()) return -1.0;
            var largest_diameter = diameters.Select(x => x.Value).Max();
            var largest_ids = diameters.Where(x => x.Value == largest_diameter).Select(x => x.Key).ToList();
            largest_conduits.AddRange(largest_ids);
            return largest_diameter;
        }

        /// <summary>
        /// Gets the smallest diameter of all the conduits connected to this junction box
        /// </summary>
        public double GetsmallestDiameter(Document doc, out List<ElementId> smallest_conduits)
        {
            smallest_conduits = new List<ElementId>();
            var diameters = GetAllDiameters(doc);
            if(!diameters.Any()) return -1.0;
            var smallest_diameter = diameters.Select(x => x.Value).Min();
            var smallest_ids = diameters.Where(x => x.Value == smallest_diameter).Select(x => x.Key).ToList();
            smallest_conduits.AddRange(smallest_ids);
            return smallest_diameter;
        }

        private static bool IsConnectedTo(Document doc, Connector c, Connector c2) {

            if(c.ConnectorType == ConnectorType.Logical || c2.ConnectorType == ConnectorType.Logical) return false;
            bool s = false;

            try {
                s = c.Origin.IsAlmostEqualTo(c2.Origin, ConnectorTolerance);
            }
            catch {
                var c1type = Enum.GetName(typeof(ConnectorType), c.ConnectorType);
                var c2type = Enum.GetName(typeof(ConnectorType), c2.ConnectorType);
                throw new Exception( "Connector 1 Type: " + c1type + " | Connector 2 Type" + c2type);
            }
            return s;
        }
    }
}   