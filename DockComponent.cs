﻿// Decompiled with JetBrains decompiler
// Type: ValheimRAFT.DockComponent
// Assembly: ValheimRAFT, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B1A8BB6C-BD4E-4881-9FD4-7E1D68B1443D
// Assembly location: C:\Users\Frederick Engelhardt\Downloads\ValheimRAFT 1.4.9-1136-1-4-9-1692901079\ValheimRAFT\ValheimRAFT.dll

using UnityEngine;
using ValheimRAFT.Util;

namespace ValheimRAFT
{
  public class DockComponent : MonoBehaviour
  {
    private DockComponent.DockState m_dockState = DockComponent.DockState.None;
    private float m_dockingStrength = 1f;
    private GameObject m_dockedObject;
    private Rigidbody m_dockedRigidbody;
    private ZNetView m_nview;
    public Transform m_dockLocation;
    public Transform m_dockExit;

    public void Awake() => this.m_nview = ((Component) this).GetComponent<ZNetView>();

    public void FixedUpdate()
    {
      if (!Object.op_Implicit((Object) this.m_dockedRigidbody))
        return;
      if (this.m_dockState == DockComponent.DockState.EnteringDock)
      {
        this.PushToward(this.m_dockLocation);
      }
      else
      {
        if (this.m_dockState != DockComponent.DockState.LeavingDock)
          return;
        this.PushToward(this.m_dockExit);
      }
    }

    private void PushToward(Transform target)
    {
      Vector3 vector3 = Vector3.op_Subtraction(((Component) target).transform.position, ((Component) this.m_dockedRigidbody).transform.position);
      this.m_dockedRigidbody.AddForce(Vector3.op_Multiply(((Vector3) ref vector3).normalized, this.m_dockingStrength), (ForceMode) 2);
    }

    public void OnTriggerEnter(Collider other)
    {
      if (!Object.op_Implicit((Object) this.m_dockedObject) || !this.CanDock(other))
        return;
      this.Dock(other);
    }

    private void Dock(Collider other)
    {
      ZNetView componentInParent = ((Component) other).GetComponentInParent<ZNetView>();
      if (!Object.op_Implicit((Object) componentInParent) || !componentInParent.IsOwner())
        return;
      Rigidbody component = ((Component) componentInParent).GetComponent<Rigidbody>();
      if (!Object.op_Implicit((Object) component))
        return;
      int persistantId = ZDOPersistantID.Instance.GetOrCreatePersistantID(componentInParent.m_zdo);
      this.m_dockedObject = ((Component) componentInParent).gameObject;
      this.m_dockedRigidbody = component;
      this.m_nview.m_zdo.Set("MBDock_dockedObject", persistantId);
      this.m_dockState = DockComponent.DockState.EnteringDock;
    }

    private bool CanDock(Collider other) => ((Object) other).name.StartsWith("Karve") || ((Object) other).name.StartsWith("VikingShip");

    private enum DockState
    {
      None,
      EnteringDock,
      Docked,
      LeavingDock,
    }
  }
}
