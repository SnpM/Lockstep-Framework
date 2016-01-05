﻿using UnityEngine;
using System.Collections;
using Lockstep.Data;
namespace Lockstep
{
	public class ScanGroupHelper : BehaviourHelper
	{
		public override ushort ListenInput {
			get {
                return AbilityInterfacer.FindInterfacer(typeof (Scan)).ListenInputID;
			}
		}

		protected override void OnExecute (Lockstep.Command com)
		{
			MovementGroupHelper.Execute (com);
		}
	}
}