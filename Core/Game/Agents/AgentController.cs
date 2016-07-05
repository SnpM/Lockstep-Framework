﻿using System;
using UnityEngine;
using Lockstep.Data;
using System.Collections.Generic;
using System.Linq;
namespace Lockstep
{
    public sealed class AgentController
    {
        
        public static string[] AgentCodes;
        private static readonly Dictionary<string,ushort> CodeIndexMap = new Dictionary<string, ushort>();
        
        public static Dictionary<string,FastStack<LSAgent>> CachedAgents;
        
        public static readonly bool[] GlobalAgentActive = new bool[MaxAgents * 4];
        public static readonly LSAgent[] GlobalAgents = new LSAgent[MaxAgents * 4];
        
        private static readonly FastStack<ushort> OpenGlobalIDs = new FastStack<ushort>();

        public static ushort PeakGlobalID { get; private set; }

        public const int MaxAgents = 16384;
        private static readonly Dictionary<string,IAgentData> CodeInterfacerMap = new Dictionary<string, IAgentData>();
        public static IAgentData[] AgentData;

        public static Dictionary<ushort, FastList<bool>> TypeAgentsActive = new Dictionary<ushort, FastList<bool>>();
        public static Dictionary<ushort, FastList<LSAgent>> TypeAgents = new Dictionary<ushort, FastList<LSAgent>>();

        public static void Setup()
        {
            IAgentDataProvider database;
            if (LSDatabaseManager.TryGetDatabase<IAgentDataProvider>(out database))
            {
                AgentData = database.AgentData;

                //AgentInterfacer[] agentInters = (LSDatabaseManager.CurrentDatabase as DefaultLSDatabase).AgentData;
                AgentCodes = new string[AgentData.Length];
            
                CachedAgents = new Dictionary<string,FastStack<LSAgent>>(AgentData.Length);
            
                for (int i = 0; i < AgentData.Length; i++)
                {
                    IAgentData interfacer = AgentData [i];
                    string agentCode = interfacer.Name;
                    AgentCodes [i] = agentCode;
                
                    CachedAgents.Add(agentCode, new FastStack<LSAgent>(2));
                    CodeInterfacerMap.Add(agentCode, interfacer);
                    CodeIndexMap.Add(agentCode, (ushort)i);
                }
            } else
            {
                Debug.Log("Database does no provide AgentData. Make sure it implements IAgentDataProvider.");
            }
        }

        
        public static IAgentData GetAgentInterfacer(string agentCode)
        {
            return AgentController.CodeInterfacerMap [agentCode];
        }

        public static ushort GetAgentCodeIndex(string agentCode)
        {
            return CodeIndexMap [agentCode];
            
        }

        public static string GetAgentCode(ushort id)
        {
            return AgentCodes [id];
        }

        public static bool IsValidAgentCode(string code)
        {
            return CodeInterfacerMap.ContainsKey(code);
        }

        public static void Initialize()
        {
            InstanceManagers.FastClear();
            GlobalAgentActive.Clear();
            OpenGlobalIDs.FastClear();
            PeakGlobalID = 0;
            foreach (FastStack<LSAgent> cache in CachedAgents.Values)
            {
                for (int j = 0; j < cache.Count; j++)
                {
                    cache.innerArray [j].SessionReset();
                }
            }
            
        }

        
        public static void Deactivate()
        {
            for (int i = 0; i < PeakGlobalID; i++)
            {
                if (GlobalAgentActive [i])
                {
                    DestroyAgent(GlobalAgents [i], true);
                }
            }
        }

        private static ushort GenerateGlobalID()
        {
            if (OpenGlobalIDs.Count > 0)
            {
                return OpenGlobalIDs.Pop();
            }
            return PeakGlobalID++;
        }

        public static bool TryGetAgentInstance(int globalID, out LSAgent returnAgent)
        {
            if (GlobalAgentActive [globalID])
            {
                returnAgent = GlobalAgents [globalID];
                return true;
            }
            returnAgent = null;
            return false;
        }

        
        
        public static void Simulate()
        {
            for (int iterator = 0; iterator < PeakGlobalID; iterator++)
            {
                if (GlobalAgentActive [iterator])
                {
                    GlobalAgents [iterator].Simulate();
                }
            }
        }

        public static void LateSimulate()
        {
            for (int i = 0; i < PeakGlobalID; i++)
            {
                if (GlobalAgentActive [i])
                    GlobalAgents [i].LateSimulate();
            }
        }

        public static void Visualize()
        {
            for (int iterator = 0; iterator < PeakGlobalID; iterator++)
            {
                if (GlobalAgentActive [iterator])
                {
                    GlobalAgents [iterator].Visualize();
                }
            }
        }
        public static void ChangeController (LSAgent agent, AgentController newCont) {


            AgentController leController = agent.Controller;
            leController.LocalAgentActive [agent.LocalID] = false;
            GlobalAgentActive[agent.GlobalID] = false;
            leController.OpenLocalIDs.Add(agent.LocalID);
            OpenGlobalIDs.Add(agent.GlobalID);

            if (newCont == null) {
                agent.InitializeController(null,0,0);
            }
            else {
                agent.Influencer.Deactivate();

                newCont.AddAgent(agent);
                agent.Influencer.Initialize();

            }


        }
        public static void DestroyAgent(LSAgent agent, bool Immediate = false)
        {
          

            agent.Deactivate(Immediate);

            ushort agentCodeID = AgentController.GetAgentCodeIndex (agent.MyAgentCode);

            TypeAgentsActive[agentCodeID][agent.TypeIndex] = false;

            ChangeController (agent, null);

        }

        public static void CacheAgent(LSAgent agent)
        {
            CachedAgents [agent.MyAgentCode].Add(agent);
        }

        private static void UpdateDiplomacy(AgentController newCont)
        {
            for (int i = 0; i < InstanceManagers.Count; i++)
            {
                InstanceManagers [i].SetAllegiance(newCont, AllegianceType.Neutral);
            }
        }

        
        public static int GetStateHash()
        {
            int operationToggle = 0;
            int hash = LSUtility.PeekRandom(int.MaxValue);
            for (int i = 0; i < PeakGlobalID; i++)
            {
                if (GlobalAgentActive [i])
                {
                    LSAgent agent = GlobalAgents [i];
                    int n1 = agent.Body._position.GetHashCode() + agent.Body._rotation.GetHashCode();
                    switch (operationToggle)
                    {
                        case 0:
                            hash ^= n1;
                            break;
                        case 1:
                            hash += n1;
                            break;
                        default:
                            hash ^= n1 * 3;
                            break;
                    }
                    operationToggle++;
                    if (operationToggle >= 2)
                    {
                        operationToggle = 0;
                    }
                    if (agent.Body.IsNotNull())
                    {
                        hash ^= agent.Body._position.GetHashCode();
                        hash ^= agent.Body._position.GetHashCode();
                    }
                }
            }
            
            
            return hash;
        }

        public static FastList<AgentController> InstanceManagers = new FastList<AgentController>();
        public readonly FastBucket<LSAgent> SelectedAgents = new FastBucket<LSAgent>();

        public bool SelectionChanged { get; set; }

        public readonly LSAgent[] LocalAgents = new LSAgent[MaxAgents];
        public readonly bool[] LocalAgentActive = new bool[MaxAgents];

        public byte ControllerID { get; private set; }

        public ushort PeakLocalID { get; private set; }

        public int PlayerIndex { get; set; }

        public bool HasTeam { get; private set; }

        public Team MyTeam { get; private set; }

        private readonly FastList<AllegianceType> DiplomacyFlags = new FastList<AllegianceType>();
        private readonly FastStack<ushort> OpenLocalIDs = new FastStack<ushort>();

        public static AgentController Create()
        {
            return new AgentController();
        }

        private AgentController()
        {
            if (InstanceManagers.Count > byte.MaxValue)
            {
                throw new System.Exception("Cannot have more than 256 AgentControllers");
            }
            OpenLocalIDs.FastClear();
            PeakLocalID = 0;
            ControllerID = (byte)InstanceManagers.Count;

            for (int i = 0; i < InstanceManagers.Count; i++)
            {
                this.SetAllegiance(InstanceManagers [i], AllegianceType.Neutral);
            }
            UpdateDiplomacy(this);

            InstanceManagers.Add(this);
            this.SetAllegiance(this, AllegianceType.Friendly);
        }

        public void AddToSelection(LSAgent agent)
        {
            SelectedAgents.Add(agent);
            SelectionChanged = true;
        }

        public void RemoveFromSelection(LSAgent agent)
        {
            SelectedAgents.Remove(agent);
            SelectionChanged = true;
        }

        private Selection previousSelection = new Selection();

        public Selection GetSelection (Command com) {
            if (com.ContainsData<Selection>() == false) {
                return previousSelection;
            }
            return com.GetData<Selection>();
        }
        public void Execute(Command com)
        {
            if (com.ContainsData<Selection>())
            {
                previousSelection = com.GetData<Selection>();
            }

            BehaviourHelperManager.Execute(com);
            Selection selection = GetSelection(com);
            for (int i = 0; i < selection.selectedAgentLocalIDs.Count; i++)
            {
                ushort selectedAgentID = selection.selectedAgentLocalIDs [i];
                if (LocalAgentActive [selectedAgentID])
                {
                    LocalAgents [selectedAgentID].Execute(com);
                }
            }
        }

        //Backward compat.
        public static Command GenerateSpawnCommand(AgentController cont, string agentCode, int count, Vector2d position)
        {
            return Lockstep.Example.ExampleSpawner.GenerateSpawnCommand(cont, agentCode, count, position);
        }
        public void AddAgent (LSAgent agent) {
            ushort localID = GenerateLocalID();
            LocalAgents [localID] = agent;
            LocalAgentActive [localID] = true;

            ushort globalID = GenerateGlobalID();
            GlobalAgentActive [globalID] = true;
            GlobalAgents [globalID] = agent;

            agent.InitializeController(this,localID, globalID);
        }
        public LSAgent CreateAgent(
            string agentCode,
            Vector2d? position = null, //nullable position
            Vector2d? rotation = null  //Nullable rotation for default parametrz
        )
        {
            Vector2d pos = position != null ? position.Value : new Vector2d(0, 0);
            Vector2d rot = rotation != null ? rotation.Value : Vector2d.radian0;


            if (!IsValidAgentCode(agentCode))
            {
                throw new System.ArgumentException(string.Format("Agent code '{0}' not found.", agentCode));
            }

           
            FastStack<LSAgent> cache = CachedAgents [agentCode];
            LSAgent curAgent = null;
            ushort agentCodeID = AgentController.GetAgentCodeIndex(agentCode);

            if (cache.IsNotNull() && cache.Count > 0)
            {
                curAgent = cache.Pop();

                TypeAgentsActive[agentCodeID][curAgent.TypeIndex] = true;
            } else
            {
                IAgentData interfacer = AgentController.CodeInterfacerMap [agentCode];

                curAgent = GameObject.Instantiate(interfacer.GetAgent().gameObject).GetComponent<LSAgent>();
                curAgent.Setup(interfacer);


                FastList<bool> typeActive;
                if (!AgentController.TypeAgentsActive.TryGetValue(agentCodeID, out typeActive)) {
                    typeActive = new FastList<bool>();
                    TypeAgentsActive.Add(agentCodeID, typeActive);
                }
                FastList<LSAgent> typeAgents;
                if (!TypeAgents.TryGetValue(agentCodeID,out typeAgents)) {
                    typeAgents = new FastList<LSAgent>();
                    TypeAgents.Add(agentCodeID, typeAgents);
                }

                curAgent.TypeIndex = (ushort)typeAgents.Count;
                typeAgents.Add (curAgent);
                typeActive.Add (true);
            }
            InitializeAgent(curAgent, pos, rot);
            return curAgent;
        }
        
        private void InitializeAgent(LSAgent agent,
                                     Vector2d position,
                                     Vector2d rotation)
        {
            AddAgent (agent);
            agent.Initialize(position, rotation);
        }

        private ushort GenerateLocalID()
        {
            if (OpenLocalIDs.Count > 0)
            {
                return OpenLocalIDs.Pop();
            } else
            {
                return PeakLocalID++;
            }
        }

        public void SetAllegiance(AgentController otherController, AllegianceType allegianceType)
        {
            while (DiplomacyFlags.Count <= otherController.ControllerID)
            {
                DiplomacyFlags.Add(AllegianceType.Neutral);
            }
            DiplomacyFlags [otherController.ControllerID] = allegianceType;
        }

        public AllegianceType GetAllegiance(AgentController otherController)
        {
            return HasTeam && otherController.HasTeam ? MyTeam.GetAllegiance(otherController) : DiplomacyFlags [otherController.ControllerID];
        }

        public AllegianceType GetAllegiance(byte controllerID)
        {
            if (HasTeam)
            {
                //TODO: Team allegiance
            }

            return DiplomacyFlags [controllerID];
        }

        public void JoinTeam(Team team)
        {
            MyTeam = team;
            HasTeam = true;
        }

        public void LeaveTeam()
        {
            HasTeam = false;
        }
    }

    [System.Flags]
    public enum AllegianceType : int
    {
        Neutral = 1 << 0,
        Friendly = 1 << 1,
        Enemy = 1 << 2,
        All = ~0
    }
  
}