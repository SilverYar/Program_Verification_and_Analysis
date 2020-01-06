using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.Msagl.Drawing;
using System.IO;
using Microsoft.Msagl.GraphViewerGdi;
using System.Drawing;
using System.Collections;
namespace AST
{
    class Program
    {
        class Node
        {
            public string name;
            public string content;
            public Node cond;
            public bool is_cond_node = false;
            public Microsoft.Msagl.Drawing.Node node;
        }
        class Edge
        {
            public Node source;
            public Node dest;
            public string note;
            public Microsoft.Msagl.Drawing.Edge edge;
        }
        class Graph
        {
            public ArrayList nodes;
            public ArrayList edges;
            public Dictionary<UInt64,Node> id2node;
            public Graph()
            {
                nodes = new ArrayList();
                edges = new ArrayList();
                id2node = new Dictionary<ulong, Node>();
            }
        }
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                System.Console.WriteLine("Usage:\n[progname] [source.cs]");
                return;
            }
            string source = File.ReadAllText(args[0], Encoding.UTF8);

            CSharpParseOptions options = CSharpParseOptions.Default.WithFeatures(new[] { new KeyValuePair<string, string>("flow-analysis", "") });

            var tree = CSharpSyntaxTree.ParseText(source, options);
            var compilation = CSharpCompilation.Create("c", new[] { tree });
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            Graph g = new Graph();
            UInt64 bid = 0;
            foreach (var methodBodySyntax in tree.GetCompilationUnitRoot().DescendantNodes().OfType<BaseMethodDeclarationSyntax>()) {

                //   var methodBodySyntax = tree.GetCompilationUnitRoot().DescendantNodes().OfType<BaseMethodDeclarationSyntax>().Last();
                string procname = methodBodySyntax.ToString().Substring(0, methodBodySyntax.ToString().IndexOf("{"));
                Console.WriteLine(procname);
                var cfgFromSyntax = ControlFlowGraph.Create(methodBodySyntax, model);
                Dictionary<BasicBlock, UInt64> block_ids = new Dictionary<BasicBlock, ulong>();

                foreach (var bb in cfgFromSyntax.Blocks)
                {
                    block_ids[bb] = bid;
                    
                    Node n = new Node();
                    n.name = String.Format("block_{0}:", bid);
                    n.content = "";
                    foreach (var op in bb.Operations)
                    {
                        n.content += op.Syntax.ToString()+"\n";
                    }
                    g.nodes.Add(n);
                    g.id2node[bid] = n;
                    bid += 1;
                    if (bb.ConditionKind != ControlFlowConditionKind.None && bb.BranchValue != null)
                    {
                        Node n1 = new Node();
                        n1.content = bb.BranchValue.Syntax.ToString();
                        n1.is_cond_node = true;
                        n.cond = n1;
                        n1.name = String.Format("block_{0}:", bid);
                        g.nodes.Add(n1);
                        g.id2node[bid] = n1;
                        bid += 1;

                    }
                }
                bool is_first = true;
                foreach (var bb in cfgFromSyntax.Blocks)
                {
                    var x = block_ids[bb];
                    System.Console.WriteLine(String.Format("block_{0}:", x));
                    if (is_first)
                    {
                        is_first = false;
                        g.id2node[x].content = "method "+procname + "\n"+g.id2node[x].content;
                    }
                    foreach (var op in bb.Operations) {
                        System.Console.WriteLine(op.Syntax.ToString());
                    }
                    
                    if (bb.ConditionKind == ControlFlowConditionKind.None)
                    {
                        if (bb.BranchValue != null)
                        {
                            g.id2node[x].content = g.id2node[x].content + "\n" + "return " + bb.BranchValue.Syntax.ToString();
                            System.Console.WriteLine("return "+bb.BranchValue.Syntax.ToString());
                        }
                        if (bb.FallThroughSuccessor != null)
                        {
                            Edge e = new Edge();
                            e.source = g.id2node[x];
                            var next = block_ids[bb.FallThroughSuccessor.Destination];
                            e.dest = g.id2node[next];
                            g.edges.Add(e);
                            System.Console.WriteLine(String.Format("block_{0}", block_ids[bb.FallThroughSuccessor.Destination]));
                        }
                    }
                    else if (bb.ConditionKind == ControlFlowConditionKind.WhenFalse)
                    {
                        if (bb.BranchValue != null)//!
                        {
                            //g.id2node[x].content = g.id2node[x].content + "\n" + bb.BranchValue.Syntax.ToString();
                            Edge e0 = new Edge();
                            e0.source = g.id2node[x];
                            e0.dest = g.id2node[x].cond;
                            g.edges.Add(e0);

                            Edge e1 = new Edge();
                            e1.source = e0.dest;
                            var next = block_ids[bb.ConditionalSuccessor.Destination];
                            e1.dest = g.id2node[next];
                            g.edges.Add(e1);

                            e1.note = "false"; // ??

                            Edge e2 = new Edge();
                            e2.source = e0.dest;
                            next = block_ids[bb.FallThroughSuccessor.Destination];
                            e2.dest = g.id2node[next];
                            g.edges.Add(e2);

                            e2.note = "true"; // ??

                            System.Console.WriteLine(bb.BranchValue.Syntax.ToString());
                        }//!!
                        else
                        {
                            Edge e1 = new Edge();
                            e1.source = g.id2node[x];
                            var next = block_ids[bb.ConditionalSuccessor.Destination];
                            e1.dest = g.id2node[next];
                            g.edges.Add(e1);

                            e1.note = bb.BranchValue.Syntax.ToString();

                            Edge e2 = new Edge();
                            e2.source = g.id2node[x];
                            next = block_ids[bb.FallThroughSuccessor.Destination];
                            e2.dest = g.id2node[next];
                            g.edges.Add(e2);

                            e2.note = "not " + bb.BranchValue.Syntax.ToString();

                            System.Console.WriteLine(String.Format("{0}?block_{1}:block_{2}", bb.BranchValue.Syntax.ToString(),
                                block_ids[bb.ConditionalSuccessor.Destination], block_ids[bb.FallThroughSuccessor.Destination]));
                        }
                    }
                    else
                    {
                        if (bb.BranchValue != null)//!
                        {
                            Edge e0 = new Edge();
                            e0.source = g.id2node[x];
                            e0.dest = g.id2node[x].cond;
                            g.edges.Add(e0);

                            Edge e1 = new Edge();
                            e1.source = e0.dest;
                            var next = block_ids[bb.ConditionalSuccessor.Destination];
                            e1.dest = g.id2node[next];
                            g.edges.Add(e1);

                            e1.note = "true"; //??

                            Edge e2 = new Edge();
                            e2.source = e0.dest;
                            next = block_ids[bb.FallThroughSuccessor.Destination];
                            e2.dest = g.id2node[next];
                            g.edges.Add(e2);

                            e2.note = "false"; //??

                            System.Console.WriteLine(bb.BranchValue.Syntax.ToString());
                        }//!!
                        else
                        {
                            Edge e1 = new Edge();
                            e1.source = g.id2node[x];
                            var next = block_ids[bb.ConditionalSuccessor.Destination];
                            e1.dest = g.id2node[next];
                            g.edges.Add(e1);

                            e1.note = "not " + bb.BranchValue.Syntax.ToString();

                            Edge e2 = new Edge();
                            e2.source = g.id2node[x];
                            next = block_ids[bb.FallThroughSuccessor.Destination];
                            e2.dest = g.id2node[next];
                            g.edges.Add(e2);

                            e2.note = bb.BranchValue.Syntax.ToString();

                            System.Console.WriteLine(String.Format("!{0}?block_{1}:block_{2}", bb.BranchValue.Syntax.ToString(),
                                block_ids[bb.ConditionalSuccessor.Destination], block_ids[bb.FallThroughSuccessor.Destination]));
                        }
                    }
                }
            }
            Microsoft.Msagl.Drawing.Graph draw_graph = new Microsoft.Msagl.Drawing.Graph("");
            /*foreach(var nd in g.nodes)
            {
                Microsoft.Msagl.Drawing.Node n = new Microsoft.Msagl.Drawing.Node(((Node)nd).name);
                n.LabelText = ((Node)nd).content;
                draw_graph.AddNode(n);
                ((Node)nd).node = n;
            }
            foreach(var ed in g.edges)
            {
                Edge cur_e = (Edge)ed;
                var src = cur_e.source.node;
                var dst = cur_e.dest.node;
                Microsoft.Msagl.Drawing.Edge e = new Microsoft.Msagl.Drawing.Edge(src, dst, ConnectionToGraph.Connected);
                draw_graph.AddEdge(src.Id, ((Edge)ed).note, dst.Id);
            }*/
            //!
            foreach (var ed in g.edges)
            {
                if (((Edge)ed).source.node == null)
                {
                    string src_s = ((Edge)ed).source.name + "\n" + ((Edge)ed).source.content;
                    Microsoft.Msagl.Drawing.Node n = new Microsoft.Msagl.Drawing.Node(src_s);
                    if (((Edge)ed).source.is_cond_node)
                        n.Attr.Shape = Shape.Diamond;
                    draw_graph.AddNode(n);
                    ((Edge)ed).source.node = n;
                }
                if (((Edge)ed).dest.node == null)
                {
                    string src_s = ((Edge)ed).dest.name + "\n" + ((Edge)ed).dest.content;
                    Microsoft.Msagl.Drawing.Node n = new Microsoft.Msagl.Drawing.Node(src_s);
                    if (((Edge)ed).dest.is_cond_node)
                        n.Attr.Shape = Shape.Diamond;
                    draw_graph.AddNode(n);
                    ((Edge)ed).dest.node = n;
                }
            }//!!
            foreach (var ed in g.edges)
            {
                string src_s = ((Edge)ed).source.name+ "\n" + ((Edge)ed).source.content;
                string edge_s = ((Edge)ed).note;
                if (edge_s == null)
                {
                    edge_s = "";
                }
                string dest_s = ((Edge)ed).dest.name + "\n" + ((Edge)ed).dest.content;

                draw_graph.AddEdge(src_s,edge_s,dest_s);
//                break;
            }
            Microsoft.Msagl.GraphViewerGdi.GraphRenderer renderer = new Microsoft.Msagl.GraphViewerGdi.GraphRenderer(draw_graph);
            renderer.CalculateLayout();
            int width = (int)(draw_graph.Width);
            Bitmap bitmap = new Bitmap(width, (int)(draw_graph.Height * (width / draw_graph.Width)));
            renderer.Render(bitmap);
            bitmap.Save("test.png");


        }
    }
}
