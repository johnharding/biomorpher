﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using System.Windows.Forms;
using System.Drawing;
using Rhino.Geometry;
using Biomorpher.IGA;
using Grasshopper.Kernel.Special;
using GalapagosComponents;
using Grasshopper.Kernel.Data;

namespace Biomorpher
{
    /// <summary>
    /// The Grasshopper component
    /// </summary>
    public class BiomorpherReader: GH_Component
    {
        public Grasshopper.GUI.Canvas.GH_Canvas canvas;
        private static readonly object syncLock = new object();

        bool isActive { get; set; }

        //private List<GH_NumberSlider> sliders = new List<GH_NumberSlider>();
        //private List<GalapagosGeneListObject> genepools = new List<GalapagosGeneListObject>();

        private int branch;
        private int generation;
        private int design;

        private BiomorpherDataParam myParam;
        private BiomorpherData solutionData;

        /// <summary>
        /// Main constructor
        /// </summary>
        public BiomorpherReader()
            : base("BiomorpherReader", "BiomorpherReader", "Uses Biomorpher data to display paramter states", "Params", "Util")
        {    
            canvas = Instances.ActiveCanvas;
            this.IconDisplayMode = GH_IconDisplayMode.icon;
            isActive = true;
        }

        /// <summary>
        /// Register component inputs
        /// </summary>
        /// <param name="pm"></param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pm)
        {
            myParam = new BiomorpherDataParam();
            pm.AddParameter(myParam, "Solution", "Solution", "Biomorpher Solution Data for use in reader", GH_ParamAccess.item);
            pm.AddIntegerParameter("Branch", "Branch", "Generation branch (usually zero unless you conducted multiple runs)", GH_ParamAccess.item, 0);
            pm.AddIntegerParameter("Generation", "Generation", "Generation (epoch)", GH_ParamAccess.item, 0);
            pm.AddIntegerParameter("Design", "Design", "Design ID from the population", GH_ParamAccess.item, 0);
        }
        
        /// <summary>
        /// Register component outputs
        /// </summary>
        /// <param name="pm"></param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pm)
        {
            //pm[1].Simplify = true;
            pm.AddGenericParameter("out", "out", "out", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Grasshopper solveinstance
        /// </summary>
        /// <param name="DA"></param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            
            // Get Solution and data
            BiomorpherGoo temp = new BiomorpherGoo();
            if (!DA.GetData("Solution", ref temp)) { return; }
            solutionData = temp.Value;

            if (!DA.GetData<int>("Branch", ref branch)) { return; };
            if (!DA.GetData<int>("Generation", ref generation)) { return; };
            if (!DA.GetData<int>("Design", ref design)) { return; };

            // Output (to be deleted)
            // int lastGeneration = solutionData.historicData.get_Branch(new GH_Path(branch, generation, design)).Count;
            // DA.SetData(0, lastGeneration);

            // Check to see if we have anything at all (population is the lowest possible thing in the hierarchy)
            if (solutionData.historicData == null)
            {
               this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No histric gene data in the solution!");
               return;
            }

            // Display the number of designs in a typical population
            else
            {
                // Needs updating
               Message = "Population size = " + solutionData.historicData.Branches.Count;
            }

            if (isActive && solutionData.historicData.get_Branch(new GH_Path(branch, generation, design)) != null)
            {

                //We need a list of genes for the selected design
                List<double> genes = new List<double>();


                for (int i = 0; i < solutionData.historicData.get_Branch(new GH_Path(branch, generation, design)).Count; i++)
                {
                    double myDouble;
                    GH_Convert.ToDouble(solutionData.historicData.get_Branch(new GH_Path(branch, generation, design))[i], out myDouble, GH_Conversion.Primary);
                    genes.Add(myDouble);
                }

                // Set up some local sliders and genepools
                List<GH_NumberSlider> theSliders = new List<GH_NumberSlider>();
                List<GalapagosGeneListObject> theGenePools = new List<GalapagosGeneListObject>();

                bool flag = false;

                // Note that the sliders and genepools are stored in two branches of a GH_Structure
                try
                {
                    // Get sliders
                    List<GH_Guid> sliderList = new List<GH_Guid>();

                    foreach (GH_Guid x in solutionData.genoGuids.get_Branch(0))
                    {
                        GH_NumberSlider slidy = OnPingDocument().FindObject<GH_NumberSlider>(x.Value, true);
                        if(slidy!=null) theSliders.Add(slidy);
                    }

                    // Get genepools
                    foreach (GH_Guid x in solutionData.genoGuids.get_Branch(1))
                    {
                        GalapagosGeneListObject pooly = OnPingDocument().FindObject<GalapagosGeneListObject>(x.Value, true);
                        if (pooly != null) theGenePools.Add(pooly);
                    }
                }
                catch
                {
                    flag = true;
                }


                if (flag)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Getting sliders and/or genepools from the canvas was unsuccesful. Have they been modified?");
                    return;
                }


                canvas.Document.Enabled = false;
                SetSliders(genes, theSliders, theGenePools);
                canvas.Document.Enabled = true;

                if(theGenePools.Count > 0) isActive = false;
            }

            else
            {
                // Turn the thing back on without setting all the sliders etc.
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No historic design found at this reference");
                isActive = true;
            }
            
        }


        /// <summary>
        /// Sets the current slider values for a geven input chromosome
        /// </summary>
        /// <param name="genes"></param>
        /// <param name="sliders"></param>
        /// <param name="genePools"></param>
        public void SetSliders(List<double> genes, List<GH_NumberSlider> sliders, List<GalapagosGeneListObject> genePools)
        {

            int sCount = sliders.Count;

            for (int i = 0; i < sCount; i++)
            {
                double min = (double)sliders[i].Slider.Minimum;
                double max = (double)sliders[i].Slider.Maximum;
                double range = max - min;

                sliders[i].Slider.Value = (decimal)(genes[i] * range + min);
            }

            // Set the gene pool values
            // Note that we use the back end of the genes, beyond the slider count
            int geneIndex = sCount;

            for (int i = 0; i < genePools.Count; i++)
            {
                for (int j = 0; j < genePools[i].Count; j++)
                {
                    Decimal myDecimal = System.Convert.ToDecimal(genes[geneIndex]);
                    genePools[i].set_NormalisedValue(j, myDecimal);

                    geneIndex++;
                }
                genePools[i].ExpireSolution(true);
            }
        }


        /// <summary>
        /// Gets the component guid
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("6DD8D538-EF87-453B-BF1B-77AD316FF9A0"); }
        }

        /// <summary>
        /// Create bespoke component attributes
        /// </summary>
        public override void CreateAttributes()
        {
            m_attributes = new BiomorpherReaderAttributes(this);
        }

        /// <summary>
        /// Locate the component with the rest of the rif raf
        /// </summary>
        public override GH_Exposure Exposure
        {
            get
            {
                return GH_Exposure.senary;
            }
        }

        /// <summary>
        /// Icon icon what a lovely icon
        /// </summary>
        protected override Bitmap Icon
        {
            get
            {
                return Properties.Resources.BiomorpherReaderIcon_24;
            }
        }

        /// <summary>
        /// Extra fancy menu items
        /// </summary>
        /// <param name="menu"></param>
        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, @"Github source", GotoGithub);
        }

        /// <summary>
        /// Dare ye go to github?
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GotoGithub(Object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(@"https://github.com/johnharding/Biomorpher");
        }

        
    }
}
