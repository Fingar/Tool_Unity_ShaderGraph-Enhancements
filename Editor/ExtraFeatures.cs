using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEditor.ShortcutManagement;

/*
	Forked by: Fingar (https://twitter.com/FingarB)

	Disabled hotkey rebinding, and changed all hotkeys to follow the pattern A for Add, M for Multiply, U for UV, etc.
	Some hotkeys require the have the Shift or Ctrl+Shift modifiers to make sure they don't collide with default Unity hotkeys.	
*/


/*
Author : Cyanilux (https://twitter.com/Cyanilux)
Github Repo : https://github.com/Cyanilux/ShaderGraphVariables

Extra Features :
	- Colour of groups can be changed
		- Right-click group the group name and select Edit Group Colour
		- A colour picker should open, allowing you to choose a colour (including alpha)
		- Behind the scenes this creates a hidden Color node in the group. It can only be moved with
			the group so try to move the group as a whole rather than nodes inside it.
	- 'Port Swap' Hotkey
		- "S" key will swap the first two ports (usually A and B) on all currently selected nodes
		- Enabled for nodes : Add, Subtract, Multiply, Divide, Maximum, Minimum, Lerp, Inverse Lerp, Step and Smoothstep
		- Note : It needs at least one connection to swap. If both ports are empty, it won't swap the values as it doesn't 
			update properly and I couldn't find a way to force-update it.
	- 'Add Node' Hotkeys (Default : Alpha Number keys, 1 to 0)
		- Bind 10 hotkeys for quickly adding specific nodes to the graph (at mouse position)
		- Defaults are :
			- 1 : Add
			- 2 : Subtract
			- 3 : Multiply
			- 4 : Lerp
			- 5 : Split
			- 6 : One Minus
			- 7 : Negate
			- 8 : Absolute
			- 9 : Step
			- 0 : Smoothstep
		- To change nodes : Tools → SGVariablesExtraFeatures → Rebind Node Bindings
	- To edit keybindings : Edit → Shortcuts (search for SGVariables)
		- Note, try to avoid conflicts with SG's hotkeys (mainly A, F and O) as those can't be rebound
		- https://www.cyanilux.com/tutorials/intro-to-shader-graph/#shortcuts

*/

using static Cyan.SGVariables;

namespace Cyan {

	public class ExtraFeatures {

		#region Extra Features (Swap Command)
		[MenuItem("Tools/ShaderGraphEnhancements/Swap Ports On Selected Nodes #s")]
		private static void SwapPortsCommand() {
			if (!sgHasFocus || graphView == null) return;
			if (debugMessages) Debug.Log("Swap Ports");

			List<ISelectable> selected = graphView.selection;
			foreach (ISelectable s in selected) {
				if (s is Node) {
					Node node = (Node)s;
					var materialNode = NodeToSGMaterialNode(node);
					string type = materialNode.GetType().ToString();
					type = type.Substring(type.LastIndexOf('.') + 1);

					if (type == "AddNode" || type == "SubtractNode" ||
						type == "MultiplyNode" || type == "DivideNode" ||
						type == "MaximumNode" || type == "MinimumNode" ||
						type == "LerpNode" || type == "InverseLerpNode" ||
						type == "StepNode" || type == "SmoothstepNode") {
						var inputPorts = GetInputPorts(node);
						Port a = inputPorts.AtIndex(0);
						Port b = inputPorts.AtIndex(1);

						// Swap connections
						Port connectedA = GetConnectedPort(a);
						Port connectedB = GetConnectedPort(b);
						DisconnectAllInputs(node);
						if (connectedA != null) Connect(connectedA, b);
						if (connectedB != null) Connect(connectedB, a);

						// Swap values (the values used if no port is connected)
						if (connectedA != null || connectedB != null) {
							// Node doesn't update properly unless at least one connection swaps
							var aSlot = GetMaterialSlot(a);
							var bSlot = GetMaterialSlot(b);
							var valueProperty = aSlot.GetType().GetProperty("value");
							var valueA = valueProperty.GetValue(aSlot);
							var valueB = valueProperty.GetValue(bSlot);
							valueProperty.SetValue(aSlot, valueB);
							valueProperty.SetValue(bSlot, valueA);
						}

						//if (connectedA == null && connectedB == null) {
						// If there's no connections we need to update values ourself
						//graphDataType.GetMethod("ValidateGraph").Invoke(graphData, null);
						//abstractMaterialNodeType.GetMethod("Dirty").Invoke(materialNode, new object[]{1});
						//graphDataType.GetMethod("ValidateGraph").Invoke(graphData, null);
						//}
					}
				}
			}
		}

		#endregion

		#region Extra Features (Add Node Commands)
		private class AddNodeType{
			public Type type;
			public string subgraphGUID; // guid if type is UnityEditor.ShaderGraph.SubGraphNode

			public AddNodeType(Type type, string subgraphGUID){
				this.type = type;
				this.subgraphGUID = subgraphGUID;
			}
		}
        
		private static AddNodeType[] addNodeTypes = new AddNodeType[10];
		private static readonly string[] addNodeDefaults = new string[]{
			"Add",
			"Subtract",
			"Multiply",
			"Lerp",
			"Split",
			"OneMinus",
			"Negate",
			"Absolute",
			"Step",
			"Smoothstep"
		};

		#region Extra Features (Rebind)
        /*
		private class RebindWindow : EditorWindow {

			private string shortcutID = "Main Menu/Tools/ShaderGraphEnhancements/Add Node ";
			private string[] shortcuts;
			private string[] values;

			//private IShortcutManager shortcutManager;

			void OnEnable() {
				IShortcutManager shortcutManager = ShortcutManager.instance;
				shortcuts = new string[10];
				values = new string[10];
				for (int i = 0; i < 10; i++) {
					ShortcutBinding shortcut = shortcutManager.GetShortcutBinding(shortcutID + (i + 1));
					shortcuts[i] = shortcut.ToString();

					string s = EditorPrefs.GetString("CyanSGVariables_Node" + (i + 1));
					if (s == null || s == "") {
						s = addNodeDefaults[i];
					}
					values[i] = s;
				}
			}

			void OnGUI() {
				EditorGUILayout.LabelField("Add Node Bindings", EditorStyles.boldLabel);
				EditorGUILayout.LabelField("Note : You can also change the hotkeys in Edit -> Shortcuts, search for SGVariables", EditorStyles.wordWrappedLabel);
				EditorGUILayout.LabelField("The values here should match the class used by a node. Should be the same as the node name, without spaces. "
					+ "Won't support pipeline-specific nodes, only searches the UnityEngine.ShaderGraph namespace.", EditorStyles.wordWrappedLabel);
				EditorGUILayout.LabelField("Also supports 'RegisterVariable' and 'GetVariable', for other SubGraphs use \"SubGraph(<guid>)\", "
					+ "where guid is the string located at the top of the .meta file associated with the SubGraph.", EditorStyles.wordWrappedLabel);
				for (int i = 0; i < 10; i++) {
					EditorGUI.BeginChangeCheck();
					string s = EditorGUILayout.TextField("Node Binding " + (i + 1) + " (" + shortcuts[i] + ")", values[i]);
					if (EditorGUI.EndChangeCheck()) {
						EditorPrefs.SetString("CyanSGVariables_Node" + (i + 1), System.Text.RegularExpressions.Regex.Replace(s, @"\s+", ""));
						values[i] = s;
						ClearTypes();
					}
				}
				EditorGUILayout.LabelField("To reset to defaults, leave fields blank", EditorStyles.wordWrappedLabel);
			}

			private void ClearTypes() {
				addNodeTypes = new AddNodeType[10];
			}
		}
        */

        /*
		[MenuItem("Tools/ShaderGraphEnhancements/SGVariables/ExtraFeatures/Remap Node Bindings", false, 0)]
		private static void RebindNodes() {
			RebindWindow window = (RebindWindow)EditorWindow.GetWindow(typeof(RebindWindow));
			window.Show();
		}
        */

        [MenuItem("Tools/ShaderGraphEnhancements/Add int-node _0", false, 0)]
        private static void AddNodeCommandInt() { AddNodeCommand("Integer"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add float-node _1", false, 0)]
        private static void AddNodeCommandFloat() { AddNodeCommand("Vector1"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Vector2-node _2", false, 0)]
        private static void AddNodeCommandVector2() { AddNodeCommand("Vector2"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Vector3-node _3", false, 0)]
        private static void AddNodeCommandVector3() { AddNodeCommand("Vector3"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Vector4-node _4", false, 0)]
        private static void AddNodeCommandVector4() { AddNodeCommand("Vector4"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Color-node _5", false, 0)]
        private static void AddNodeCommandColor() { AddNodeCommand("Color"); }


        [MenuItem("Tools/ShaderGraphEnhancements/Add Add-node _A", false, 100)]
        private static void AddNodeCommandAdd() { AddNodeCommand("Add"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Subtract-node _S", false, 100)]
        private static void AddNodeCommandSubtract() { AddNodeCommand("Subtract"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Multiply-node _M", false, 100)]
        private static void AddNodeCommandMultiply() { AddNodeCommand("Multiply"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Divide-node _D", false, 100)]
        private static void AddNodeCommandDivide() { AddNodeCommand("Divide"); }


        [MenuItem("Tools/ShaderGraphEnhancements/Add Lerp-node _L", false, 200)]
        private static void AddNodeCommandLerp() { AddNodeCommand("Lerp"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Normalize-node _N", false, 200)]
        private static void AddNodeCommandNormalize() { AddNodeCommand("Normalize"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add OneMinus-node _O", false, 200)]
        private static void AddNodeCommandOneMinus() { AddNodeCommand("OneMinus"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Power-node #E", false, 200)] // Shift+E
        private static void AddNodeCommandPower() { AddNodeCommand("Power"); }
        [MenuItem("Tools/ShaderGraphEnhancements/Add SquareRoot-node #Q", false, 200)]   // Shift+Q
        private static void AddNodeCommandSquareRoot() { AddNodeCommand("SquareRoot"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Modulo-node #M", false, 100)]   // Shift+M
        private static void AddNodeCommandModulo() { AddNodeCommand("Modulo"); }


        [MenuItem("Tools/ShaderGraphEnhancements/Add Split-node _V", false, 300)]
        private static void AddNodeCommandSplit() { AddNodeCommand("Split"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Combine-node _C", false, 300)]
        private static void AddNodeCommandCombine() { AddNodeCommand("Combine"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Swizzle-node #X", false, 300)] // Shift+X
        private static void AddNodeCommandSwizzle() { AddNodeCommand("Swizzle"); }

        
        [MenuItem("Tools/ShaderGraphEnhancements/Add UV-node _U", false, 400)]
        private static void AddNodeCommandUV() { AddNodeCommand("UV"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add SampleTexture2D-node #T", false, 400)]   // Shift+T
        private static void AddNodeCommandSampleTexture2D() { AddNodeCommand("SampleTexture2D"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add Time-node %#T", false, 400)]
        private static void AddNodeCommandTime() { AddNodeCommand("Time"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add TilingAndOffset-node #W", false, 400)]   // Shift+W
        private static void AddNodeCommandTilingAndOffset() { AddNodeCommand("TilingAndOffset"); }


        [MenuItem("Tools/ShaderGraphEnhancements/Add RegisterVariable-node #R", false, 500)] // Shift+R
        private static void AddNodeCommandRegisterVariable() { AddNodeCommand("RegisterVariable"); }

        [MenuItem("Tools/ShaderGraphEnhancements/Add GetVariable-node #G", false, 500)] // Shift+G
        private static void AddNodeCommandGetVariable() { AddNodeCommand("GetVariable"); }


        [MenuItem("Tools/ShaderGraphEnhancements/Add Preview-node #P", false, 600)] // Shift+E
        private static void AddNodeCommandPreview() { AddNodeCommand("Preview"); }


        private static void AddNodeCommand(string nodeName)
        {
            if (!sgHasFocus || graphView == null) return;

            string node = nodeName;
            if (string.IsNullOrEmpty(node))
            {
                return;
            }
            string subgraphGUID = null;
            if (node.Contains("SubGraph"))
            {
                int start = node.IndexOf('(');
                int end = node.LastIndexOf(')');
                int length = end - start - 1;
                subgraphGUID = node.Substring(start + 1, length);
                node = "SubGraph";
            }
            else if (node == "RegisterVariable")
            {
                subgraphGUID = "d455b29bada2b284ca73133c44fbc1ce";
                node = "SubGraph";
            }
            else if (node == "GetVariable")
            {
                subgraphGUID = "5951f0cfb2fb4134ea014f63adeff8d9";
                node = "SubGraph";
            }
            string typeString = "UnityEditor.ShaderGraph." + node + "Node";
            var type = new AddNodeType(sgAssembly.GetType(typeString), subgraphGUID);
            Vector2 mousePos = Event.current?.mousePosition ?? Vector2.zero;
            mousePos.y -= 35;
            Matrix4x4 matrix = graphView.viewTransform.matrix.inverse;
            Rect r = new Rect();
            r.position = matrix.MultiplyPoint(mousePos);
            AddNode(type, r);
        }

        #endregion
        /*
        [MenuItem("Tools/ShaderGraphEnhancements/Add Node 1 _1")]
		private static void AddNodeCommand1() { AddNodeCommand(1); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 2 _2")]
		private static void AddNodeCommand2() { AddNodeCommand(2); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 3 _3")]
		private static void AddNodeCommand3() { AddNodeCommand(3); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 4 _4")]
		private static void AddNodeCommand4() { AddNodeCommand(4); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 5 _5")]
		private static void AddNodeCommand5() { AddNodeCommand(5); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 6 _6")]
		private static void AddNodeCommand6() { AddNodeCommand(6); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 7 _7")]
		private static void AddNodeCommand7() { AddNodeCommand(7); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 8 _8")]
		private static void AddNodeCommand8() { AddNodeCommand(8); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 9 _9")]
		private static void AddNodeCommand9() { AddNodeCommand(9); }

		[MenuItem("Tools/ShaderGraphEnhancements/Add Node 10 _0")]
		private static void AddNodeCommand10() { AddNodeCommand(10); }

        private static void AddNodeCommand(int i) {
			if (!sgHasFocus || graphView == null) return;
			AddNodeType type = addNodeTypes[i - 1];
			if (type == null) {
				string node = EditorPrefs.GetString("CyanSGVariables_Node" + i);
				if (node == null || node == "") {
					// Use default
					node = addNodeDefaults[i - 1];
				}
				string subgraphGUID = null;
				if (node.Contains("SubGraph")){
					int start = node.IndexOf('(');
					int end = node.LastIndexOf(')');
					int length = end - start - 1;
					subgraphGUID = node.Substring(start+1, length);
					node = "SubGraph";
				}else if (node == "RegisterVariable"){
					subgraphGUID = "d455b29bada2b284ca73133c44fbc1ce";
					node = "SubGraph";
				}else if (node == "GetVariable"){
					subgraphGUID = "5951f0cfb2fb4134ea014f63adeff8d9";
					node = "SubGraph";
				}
				string typeString = "UnityEditor.ShaderGraph." + node + "Node";
				type = new AddNodeType(sgAssembly.GetType(typeString), subgraphGUID);
				addNodeTypes[i - 1] = type;
				if (type == null) {
					Debug.LogWarning("Type " + typeString + " does not exist");
				}
			}
			Vector2 mousePos = Event.current.mousePosition;
			mousePos.y -= 35;
			Matrix4x4 matrix = graphView.viewTransform.matrix.inverse;
			Rect r = new Rect();
			r.position = matrix.MultiplyPoint(mousePos);
			AddNode(type, r);
		}
        */

        private static PropertyInfo drawStateProperty;
		private static PropertyInfo positionProperty;
		private static PropertyInfo expandedProperty;
		private static PropertyInfo subGraphAssetProperty;

		private static MethodInfo addNodeMethod;

		private static void AddNode(AddNodeType addNodeType, Rect position) {
			if (addNodeType == null) return;
			Type type = addNodeType.type;
			if (type == null) return;
			var nodeToAdd = Activator.CreateInstance(type);
			if (nodeToAdd == null) {
				Debug.LogWarning("Could not create node of type " + type);
			}

			// https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.shadergraph/Editor/Data/Nodes/AbstractMaterialNode.cs

			if (drawStateProperty == null) {
				drawStateProperty = abstractMaterialNodeType.GetProperty("drawState", bindingFlags); // Type : DrawState
			}

			var drawState = drawStateProperty.GetValue(nodeToAdd);
			if (positionProperty == null)
				positionProperty = drawState.GetType().GetProperty("position", bindingFlags);
			if (expandedProperty == null)
				expandedProperty = drawState.GetType().GetProperty("expanded", bindingFlags);

			positionProperty.SetValue(drawState, position);
			drawStateProperty.SetValue(nodeToAdd, drawState);

			// Handle SubGraph GUID
			string subgraphGUID = addNodeType.subgraphGUID;
			if (subgraphGUID != null){
				if (subGraphAssetProperty == null) subGraphAssetProperty = type.GetProperty("asset");
				object asset = GetSubGraphAsset(subgraphGUID);
				if (asset != null) subGraphAssetProperty.SetValue(nodeToAdd, asset);
			}

			// GraphData.AddNode(abstractMaterialNode)
			if (addNodeMethod == null)
				addNodeMethod = graphDataType.GetMethod("AddNode", bindingFlags);

			addNodeMethod.Invoke(graphData, new object[] { nodeToAdd });

			if (debugMessages) Debug.Log("Added Node of Type " + type.ToString());
		}

		// Support SubGraphs
		private static Type subGraphAssetType;
		public static object GetSubGraphAsset(string guidString){
			if (subGraphAssetType == null) subGraphAssetType = sgAssembly.GetType("UnityEditor.ShaderGraph.SubGraphAsset");
			return AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guidString), subGraphAssetType);
		}
		#endregion

		#region Extra Features (Group Colours)

		private static string GetSerializedValue(Node node) {
			object materialNode = NodeToSGMaterialNode(node);
			if (materialNode != null) {
				string[] synonyms = GetSerializedValues(materialNode);
				if (synonyms != null && synonyms.Length > 0) {
					return synonyms[0];
				}
			}
			return "";
		}

		private static void SetSerializedValue(Node node, string key) {
			object materialNode = NodeToSGMaterialNode(node);
			SetSerializedValues(materialNode, new string[] { key });
		}

		internal static void UpdateExtraFeatures() {
			if (!sgHasFocus) return;
            //if (loadVariables) { // (first time load, but we kinda need to constantly check as groups could be copied)
            // Load Group Colours
            graphView.nodes.ForEach((Node node) => {
				if (node.title.Equals("Color") && node.visible) {
					if (GetSerializedValue(node) == "GroupColor") {
						var scope = node.GetContainingScope();
						if (scope == null) {
							// Node is not in group, the group may have been deleted.
							// GraphData.RemoveNode(abstractMaterialNode);
							RemoveNode(node);
							return;
						}
						node.visible = false;
						node.style.maxWidth = 100;
						SetGroupColor(scope as Group, node, GetGroupNodeColor(node));
					}
				}
			});
			//}

			// As we right-click group, add the manipulator
			List<ISelectable> selected = graphView.selection;
			bool groupSelected = false;
			foreach (ISelectable s in selected) {
				if (s is Group) {
					Group group = (Group)s;
					groupSelected = true;

					// Set selection colour (if overriden)
					if (group.style.borderTopColor != StyleKeyword.Null) {
						Color selectedColor = new Color(0.266f, 0.75f, 1, 1);
						group.style.borderTopColor = selectedColor;
						group.style.borderBottomColor = selectedColor;
						group.style.borderLeftColor = selectedColor;
						group.style.borderRightColor = selectedColor;
					}

					if (editingGroup == s) break;

					if (editingGroup != null) {
						// (if switched to a different group)
						if (editingGroup.style.borderTopColor != StyleKeyword.Null) {
							var colorNode = GetGroupColorNode(GetGroupGuid(editingGroup));
							if (colorNode != null) {
								Color groupColor = GetGroupNodeColor(colorNode);
								editingGroup.style.borderTopColor = groupColor;
								editingGroup.style.borderBottomColor = groupColor;
								editingGroup.style.borderLeftColor = groupColor;
								editingGroup.style.borderRightColor = groupColor;
							}
						}
						editingGroup.RemoveManipulator(manipulator);
					}
					group.AddManipulator(manipulator);
					editingGroup = group;
					break;
				}
			}

			if (!groupSelected && editingGroup != null) {
				// (if group was unselected)
				if (editingGroup.style.borderTopColor != StyleKeyword.Null) {
					var colorNode = GetGroupColorNode(GetGroupGuid(editingGroup));
					if (colorNode != null) {
						Color groupColor = GetGroupNodeColor(colorNode);
						editingGroup.style.borderTopColor = groupColor;
						editingGroup.style.borderBottomColor = groupColor;
						editingGroup.style.borderLeftColor = groupColor;
						editingGroup.style.borderRightColor = groupColor;
					}
				}
				editingGroup = null;
			}

			// User clicked context menu, show Color Picker
			if (editingGuid != null) {
				Node colorNode = GetGroupColorNode(editingGuid);
				editingGroupColorNode = colorNode;
				if (colorNode != null) {
					if (colorNode.visible) {
						colorNode.visible = false;
						colorNode.style.maxWidth = 100;
					}

					ShowColorPicker(GroupColourChanged, GetGroupNodeColor(editingGroupColorNode), true, false);

					editingGuid = null;
				}
			} else {
				editingGroupColorNode = null;
			}
		}

		private static Type colorPickerType;
		private static void GetColorPickerType() {
			// pretty hacky
			Assembly assembly = Assembly.Load(new AssemblyName("UnityEditor"));
			colorPickerType = assembly.GetType("UnityEditor.ColorPicker");
		}

		private static MethodInfo showColorPicker;

		public static void ShowColorPicker(Action<Color> action, Color initalColor, bool showAlpha, bool hdr) {
			// Couldn't figure out a way to show the colour picker from UIElements, so... reflection!
			// Show(Action<Color> colorChangedCallback, Color col, bool showAlpha = true, bool hdr = false)

			if (colorPickerType == null) GetColorPickerType();
			if (showColorPicker == null) {
				showColorPicker = colorPickerType.GetMethod("Show",
					new Type[] { typeof(Action<Color>), typeof(Color), typeof(bool), typeof(bool) }
				);
			}
			showColorPicker.Invoke(null, new object[] { action, initalColor, showAlpha, hdr });
		}

		static void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
			evt.menu.AppendSeparator();
			evt.menu.AppendAction("Edit Group Color", OnMenuAction, DropdownMenuAction.AlwaysEnabled);
		}

		static void OnMenuAction(DropdownMenuAction action) {
			if (editingGroup == null) return;
			//Debug.Log("OnMenuAction " + editingGroup.title);

			string guid = GetGroupGuid(editingGroup);
			if (guid == null) return;

			Node colorNode = GetGroupColorNode(guid);
			if (colorNode == null) {
				// We store the group colour in a hidden colour node
				CreateGroupColorNode(editingGroup);

				// We need to wait until this node is actually created,
				// so we'll store the guid in editingGuid and handle the rest
				// in UpdateExtraFeatures()
			}

			editingGuid = guid;
		}

		private static ContextualMenuManipulator manipulator = new ContextualMenuManipulator(BuildContextualMenu);

		private static Group editingGroup;
		private static string editingGuid;
		private static Node editingGroupColorNode;

		private static PropertyInfo groupGuidField;
		private static PropertyInfo colorProperty;
		private static PropertyInfo groupProperty;
		private static PropertyInfo previewExpandedProperty;
		
		private static FieldInfo colorField;

		private static void CreateGroupColorNode(Group group) {
			var groupData = group.userData;
			if (groupData == null) return;

			var nodeToAdd = Activator.CreateInstance(colorNodeType); // Type : ColorNode

			// https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.shadergraph/Editor/Data/Nodes/AbstractMaterialNode.cs

			if (synonymsField == null) synonymsField = abstractMaterialNodeType.GetField("synonyms");
			synonymsField.SetValue(nodeToAdd, new string[] { "GroupColor" });

			if (colorProperty == null) colorProperty = colorNodeType.GetProperty("color"); // SG Color struct, not UnityEngine.Color
			var colorStruct = colorProperty.GetValue(nodeToAdd);
			if (colorField == null) colorField = colorStruct.GetType().GetField("color");
			colorField.SetValue(colorStruct, new Color(0.1f, 0.1f, 0.1f, 0.1f));
			colorProperty.SetValue(nodeToAdd, colorStruct);

			if (groupProperty == null)
				groupProperty = abstractMaterialNodeType.GetProperty("group", bindingFlags); // Type : GroupData
			if (drawStateProperty == null)
				drawStateProperty = abstractMaterialNodeType.GetProperty("drawState", bindingFlags); // Type : DrawState
			if (previewExpandedProperty == null)
				previewExpandedProperty = abstractMaterialNodeType.GetProperty("previewExpanded", bindingFlags); // Type : Bool

			previewExpandedProperty.SetValue(nodeToAdd, false);

			var drawState = drawStateProperty.GetValue(nodeToAdd);
			if (positionProperty == null)
				positionProperty = drawState.GetType().GetProperty("position", bindingFlags);
			if (expandedProperty == null)
				expandedProperty = drawState.GetType().GetProperty("expanded", bindingFlags);
			
			Rect r = group.GetPosition();
			r.x += 25;
			r.y += 60;
			positionProperty.SetValue(drawState, r);
			expandedProperty.SetValue(drawState, false);
			drawStateProperty.SetValue(nodeToAdd, drawState);
			groupProperty.SetValue(nodeToAdd, groupData);

			// GraphData.AddNode(abstractMaterialNode)
			if (addNodeMethod == null)
				addNodeMethod = graphDataType.GetMethod("AddNode", bindingFlags);

			addNodeMethod.Invoke(graphData, new object[] { nodeToAdd });
		}

		private static string GetGroupGuid(Scope scope) {
			var groupData = scope.userData;
			if (groupData == null) return null;
			if (groupGuidField == null) groupGuidField = groupData.GetType().GetProperty("objectId", bindingFlags);
			var groupGuid = (string)groupGuidField.GetValue(groupData);
			//Debug.Log("GroupGuid : " + groupGuid);
			return groupGuid;
		}

		private static Node GetGroupColorNode(string guid) {
			List<Node> nodes = graphView.nodes.ToList();
			foreach (Node node in nodes) {
				if (node.title.Equals("Color")) {
					var scope = node.GetContainingScope();
					if (scope == null) continue; // Node is not in group
					if (GetGroupGuid(scope) == guid && GetSerializedValue(node) == "GroupColor") {
						//Debug.Log("Found node in group~");
						return node;
					}
				}
			}
			return null;
		}

		private static Color GetGroupNodeColor(Node node) {
			var materialNode = NodeToSGMaterialNode(node);
			if (colorProperty == null) colorProperty = colorNodeType.GetProperty("color"); // SG Color struct, not UnityEngine.Color
			var colorStruct = colorProperty.GetValue(materialNode);
			if (colorField == null) colorField = colorStruct.GetType().GetField("color");
			return (Color)colorField.GetValue(colorStruct);
		}

		private static void SetGroupColor(Group group, Node groupNode, Color color) {
			group.style.backgroundColor = color;

			// Change border too
			group.style.borderTopColor = color;
			group.style.borderBottomColor = color;
			group.style.borderLeftColor = color;
			group.style.borderRightColor = color;

			var materialNode = NodeToSGMaterialNode(groupNode);
			if (colorProperty == null) colorProperty = colorNodeType.GetProperty("color"); // SG Color struct, not UnityEngine.Color
			var colorStruct = colorProperty.GetValue(materialNode);
			if (colorField == null) colorField = colorStruct.GetType().GetField("color");
			colorField.SetValue(colorStruct, color);
			colorProperty.SetValue(materialNode, colorStruct);

			var label = group.Query<UnityEngine.UIElements.Label>().First();
			if (color.grayscale > 0.5 && color.a > 0.4) {
				label.style.color = Color.black;
			} else {
				label.style.color = Color.white;
			}
		}

		private static void GroupColourChanged(Color color) {
			SetGroupColor(editingGroup, editingGroupColorNode, color);
		}
		#endregion
	}

}