using System.Linq;
using jp.lilxyzw.lilycalinventory.runtime;
using UnityEditor.Animations;
using UnityEngine;

namespace jp.lilxyzw.lilycalinventory
{
    // DirectBlendTreeで処理
    // AnimationHelper.Layer.cs と対になっています
    internal static partial class AnimationHelper
    {
        // lilycalInventoryの全DirectBlendTreeの追加先となるレイヤーを作成
        internal static void CreateLayer(AnimatorController controller, out BlendTree root)
        {
            // 常に1に設定されるWeightプロパティを生成
            var parameterName = "Weight";
            if(controller.parameters.Any(p => p.name == parameterName))
            {
                for(int i = 0; i < 100; i++)
                {
                    var nameTemp = $"{parameterName}_{i}";
                    if(controller.parameters.Any(p => p.name == nameTemp)) continue;
                    parameterName = nameTemp;
                    break;
                }
            }
            controller.AddParameter(parameterName, AnimatorControllerParameterType.Float);
            var parameters = controller.parameters;
            parameters[controller.parameters.Length-1].defaultFloat = 1;
            controller.parameters = parameters;

            // 各コンポーネントで生成されるBlendTreeの追加先のBlendTree
            root = new BlendTree
            {
                blendType = BlendTreeType.Direct,
                blendParameter = parameterName,
                name = "Root",
                useAutomaticThresholds = false
            };

            // BlendTreeの追加先のState
            var state = new AnimatorState
            {
                motion = root,
                name = "Root",
                writeDefaultValues = true
            };

            // Stateの追加先のStateMachine
            var stateMachine = new AnimatorStateMachine();
            stateMachine.AddState(state, stateMachine.entryPosition + new Vector3(200,0,0));
            stateMachine.defaultState = state;

            // StateMachineの追加先のLayer
            var layer = new AnimatorControllerLayer
            {
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1,
                name = ConstantValues.TOOL_NAME,
                stateMachine = stateMachine
            };
            controller.AddLayer(layer);
        }

        // 子のBlendTreeにパラメーターを設定
        internal static void SetParameter(BlendTree root)
        {
            var children = root.children;
            for(int i = 0; i < children.Length; i++)
                children[i].directBlendParameter = root.blendParameter;
            root.children = children;
        }

        internal static void AddItemTogglerTree(AnimatorController controller, AnimationClip clipDefault, AnimationClip clipChanged, string name, BlendTree root)
        {
            var layer = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                blendParameter = name,
                name = name,
                useAutomaticThresholds = true
            };

            // オンオフアニメーションを追加
            layer.AddChild(clipDefault);
            layer.AddChild(clipChanged);

            root.AddChild(layer);

            if(!controller.parameters.Any(p => p.name == name))
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        internal static void AddCostumeChangerTree(AnimatorController controller, AnimationClip[] clips, string name, BlendTree root)
        {
            var layer = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                blendParameter = name,
                name = name,
                useAutomaticThresholds = false
            };

            // 衣装の数だけアニメーションを追加
            for(int i = 0; i < clips.Length; i++)
                layer.AddChild(clips[i], i);

            root.AddChild(layer);

            if(!controller.parameters.Any(p => p.name == name))
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        internal static void AddSmoothChangerTree(AnimatorController controller, AnimationClip[] clips, float[] frames, string name, BlendTree root)
        {
            var layer = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                blendParameter = name,
                name = name,
                useAutomaticThresholds = false
            };

            // フレームの数だけアニメーションを追加
            for(int i = 0; i < clips.Length; i++)
                layer.AddChild(clips[i], frames[i]);

            root.AddChild(layer);

            if(!controller.parameters.Any(p => p.name == name))
                controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        // 複数コンポーネントから操作されるオブジェクト用
        internal static void AddMultiConditionTree(AnimatorController controller, AnimationClip clipDefault, AnimationClip clipChanged, (string name, bool toActive)[] bools, (string name, bool[] toActives)[] ints, BlendTree root, bool isActive)
        {
            var clipActive = isActive ? clipDefault : clipChanged;
            var clipInactive = isActive ? clipChanged : clipDefault;

            AddTree(root);

            void AddTree(BlendTree parent, int depth = 0, int value = 0)
            {
                var index = depth;

                if(index < bools.Length)
                {
                    AddBoolTree(parent, depth, value, bools[index].name, bools[index].toActive);
                    return;
                }
                index -= bools.Length;

                if(index < ints.Length)
                {
                    AddIntTree(parent, depth, value, ints[index].name, ints[index].toActives);
                    return;
                }
                index -= ints.Length;

                parent.AddChild(clipActive, value);
            }

            // 非アクティブにする条件をor、アクティブにする条件をandにする
            // https://github.com/lilxyzw/lilycalInventory/pull/70#issuecomment-2107029075
            void AddBoolTree(BlendTree parent, int depth, int value, string name, bool toActive)
            {
                var layer = new BlendTree
                {
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = name,
                    name = name,
                    useAutomaticThresholds = true
                };
                parent.AddChild(layer, value);

                if(!controller.parameters.Any(p => p.name == name))
                    controller.AddParameter(name, AnimatorControllerParameterType.Float);

                if(!toActive)
                {
                    AddTree(layer, depth + 1);
                    layer.AddChild(clipInactive);
                }
                else
                {
                    layer.AddChild(clipInactive);
                    AddTree(layer, depth + 1);
                }
            }

            // 非アクティブにする条件をor、アクティブにする条件をandにする
            // https://github.com/lilxyzw/lilycalInventory/pull/70#issuecomment-2107029075
            void AddIntTree(BlendTree parent, int depth, int value, string name, bool[] toActives)
            {
                var layer = new BlendTree
                {
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = name,
                    name = name,
                    useAutomaticThresholds = false
                };
                parent.AddChild(layer, value);

                if(!controller.parameters.Any(p => p.name == name))
                    controller.AddParameter(name, AnimatorControllerParameterType.Float);

                for(var i = 0; i < toActives.Length; i++)
                {
                    if(!toActives[i]) layer.AddChild(clipInactive, i);
                    else AddTree(layer, depth + 1, i);
                }
            }
        }
    }
}
