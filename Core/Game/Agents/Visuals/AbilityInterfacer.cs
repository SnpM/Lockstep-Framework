﻿using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using TypeReferences;

namespace Lockstep.Data {
#if UNITY_EDITOR
    [DataItemAttribute (
        false,
        Rotorz.ReorderableList.ReorderableListFlagsUtility.DefinedItems,
        true,
        typeof (ActiveAbility))]
#endif
    [Serializable]

    public sealed class AbilityInterfacer : ScriptDataItem {

        private static Dictionary<string,AbilityInterfacer> CodeInterfacerMap = new Dictionary<string, AbilityInterfacer>();
        private static Dictionary<Type,AbilityInterfacer>TypeInterfacerMap = new Dictionary<Type, AbilityInterfacer>();
        public static void Setup ()
	    {
            AbilityInterfacer[] interfacers = (LSDatabaseManager.CurrentDatabase as DefaultLSDatabase).AbilityData;
            for (int i = 0; i < interfacers.Length; i++) {
                AbilityInterfacer interfacer = interfacers[i];
                if (interfacer.Script.Type == null) {
                    Debug.Log(interfacer.Name);

                    //exception or ignore?
                    continue;
                }
                CodeInterfacerMap.Add(interfacer.Name, interfacer);
                TypeInterfacerMap.Add(interfacer.Script.Type, interfacer);
            }
		}

		public static AbilityInterfacer FindInterfacer (string code) {
            AbilityInterfacer output;
            if (!CodeInterfacerMap.TryGetValue(code, out output)) {
                throw new System.Exception(string.Format("AbilityInterfacer for code '{0}' not found.",code));
            }
            return output;
		}

        public static AbilityInterfacer FindInterfacer (Type type) {
            AbilityInterfacer interfacer;
            if (TypeInterfacerMap.TryGetValue (type, out interfacer))
                return interfacer;
            return null;
        }
        public static AbilityInterfacer FindInterfacer<TAbility> ()  where TAbility : ActiveAbility {
            return FindInterfacer (typeof (TAbility));
        }

        public string GetAbilityCode () {
            return this.Name;
        }
       

        [SerializeField,DataCode ("Input")]
        private string _listenInputCode;
        bool ListenInputInitialized{get; set;}
        private ushort _listenInputID;
        public string ListenInputCode {get {return _listenInputCode;}}
        public ushort ListenInputID {
            get {
                if (ListenInputInitialized) {
                    return _listenInputID;
                }
                else {
                    ListenInputInitialized = true;
                    return _listenInputID = InputCodeManager.GetCodeID (_listenInputCode);
                }
            }
        }
        [SerializeField]
		private InformationGatherType _informationGather;
		public InformationGatherType InformationGather {get {return _informationGather;}}

    }

}