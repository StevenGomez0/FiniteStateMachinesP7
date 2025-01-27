using Cinemachine.Utility;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class State
{
    //my head hurts rn :(
    public enum STATE
    {
        IDLE, PATROL, PURSUE, ATTACK, SLEEP, CUBE
    }

    public enum EVENT
    {
        ENTER, UPDATE, EXIT
    }

    public STATE name;
    protected EVENT stage;
    protected GameObject npc;
    protected Animator anim;
    protected Transform player;
    protected State nextState;
    protected NavMeshAgent agent;
    protected Transform cube;

    float visDist = 10.0f;
    float visAngle = 30.0f;
    float shootDist = 7.0f;
    float spookDist = 3.0f;

    public State(GameObject _npc, NavMeshAgent _agent, Animator _anim, Transform _player, Transform _cube)
    {
        npc = _npc;
        anim = _anim;
        player = _player;
        agent = _agent;
        cube = _cube;
        stage = EVENT.ENTER;
    }

    public virtual void Enter() { stage = EVENT.UPDATE; }
    public virtual void Update() { stage = EVENT.UPDATE; }
    public virtual void Exit() { stage = EVENT.EXIT; }

    public State Process()
    {
        if(stage == EVENT.ENTER) { Enter(); }
        if(stage == EVENT.UPDATE) { Update(); }
        if(stage == EVENT.EXIT) 
        {
            Exit(); 
            return nextState;
        }
        return this;
    }

    public bool CanSeePlayer()
    {
        Vector3 direction = player.position - npc.transform.position;
        float angle = Vector3.Angle(direction, npc.transform.forward);

        if(direction.magnitude < visDist && angle < visAngle)
        {
            return true;
        }
        return false;
    }

    public bool CanAttackPlayer()
    {
        Vector3 direction = player.position - npc.transform.position;
        if(direction.magnitude < shootDist)
        {
            return true;
        }
        return false;
    }

    public bool isSpooked()
    {
        Vector3 direction = player.position - npc.transform.position;
        float angle = Vector3.Angle(direction, npc.transform.forward);

        if (direction.magnitude < spookDist && -angle < visAngle)
        {
            return true;
        }
        return false;
    }
}

public class Idle : State
{
    public Idle(GameObject _npc, NavMeshAgent _agent, Animator _anim, Transform _player, Transform _cube)
        : base(_npc, _agent, _anim, _player, _cube)
    {
        name = STATE.IDLE;
    }

    public override void Enter()
    {
        anim.SetTrigger("isIdle");
        base.Enter();
    }

    public override void Update()
    {
        if (CanSeePlayer())
        {
            nextState = new Pursue(npc, agent, anim, player, cube);
            stage = EVENT.EXIT;
        }
        else if(Random.Range(0, 100) < 10)
        {
            nextState = new Patrol(npc, agent, anim, player, cube);
            stage = EVENT.EXIT;
        }
    }

    public override void Exit()
    {
        anim.ResetTrigger("isIdle");
        base.Exit();
    }
}

public class Patrol : State
{
    int currentIndex = -1;
    public Patrol(GameObject _npc, NavMeshAgent _agent, Animator _anim, Transform _player, Transform _cube)
        : base(_npc,_agent, _anim, _player, _cube)
    {
        name = STATE.PATROL;
        agent.speed = 2;
        agent.isStopped = false;
    }

    public override void Enter()
    {
        float lastDist = Mathf.Infinity;
        for(int i = 0; i < GameEnvironment.Singleton.Checkpoints.Count; i++)
        {
            GameObject thisWP = GameEnvironment.Singleton.Checkpoints[i];
            float distance = Vector3.Distance(npc.transform.position, thisWP.transform.position);
            if (distance < lastDist)
            {
                lastDist = distance;
                currentIndex = i-1;
            }
        }
        anim.SetTrigger("isWalking");
        base.Enter();
    }

    public override void Update()
    {
        if(agent.remainingDistance < 1)
        {
            if (currentIndex >= GameEnvironment.Singleton.Checkpoints.Count - 1)
            {
                currentIndex = 0;
            }
            else currentIndex++;

            agent.SetDestination(GameEnvironment.Singleton.Checkpoints[currentIndex].transform.position);
        }

        if (CanSeePlayer())
        {
            nextState = new Pursue(npc, agent, anim, player, cube);
            stage = EVENT.EXIT;
        }

        if (isSpooked())
        {
            nextState = new Cube(npc, agent, anim, player, cube);
            stage = EVENT.EXIT;
        }
    }

    public override void Exit()
    {
        anim.ResetTrigger("isWalking");
        base.Exit();
    }
}

public class Pursue : State
{
    public Pursue(GameObject _npc, NavMeshAgent _agent, Animator _anim, Transform _player, Transform _cube)
        : base(_npc, _agent, _anim, _player, _cube)
    { 
    name = STATE.PURSUE;
        agent.speed = 5;
        agent.isStopped = false;
    }

    public override void Enter()
    {
        anim.SetTrigger("isRunning");
        base.Enter();
    }

    public override void Update()
    {
        agent.SetDestination(player.position);
        if (agent.hasPath)
        {
            if (CanAttackPlayer())
            {
                nextState = new Attack(npc, agent, anim, player, cube);
                stage = EVENT.EXIT;
            }
            else if (!CanSeePlayer())
            {
                nextState = new Patrol(npc, agent, anim, player, cube);
                stage = EVENT.EXIT;
            }
        }
    }

    public override void Exit()
    {
        anim.ResetTrigger("isRunning");
        base.Exit();
    }
}

public class Attack : State
{
    float rotSpeed = 2.0f;
    AudioSource shoot;
    public Attack(GameObject _npc, NavMeshAgent _agent, Animator _anim, Transform _player, Transform _cube)
        : base(_npc, _agent, _anim, _player, _cube)
    {
        name = STATE.ATTACK;
        shoot = _npc.GetComponent<AudioSource>();
    }

    public override void Enter()
    {
        anim.SetTrigger("isShooting");
        agent.isStopped = true;
        shoot.Play();
        base.Enter();
    }

    public override void Update()
    {
        Vector3 direction = player.position - npc.transform.position;
        float angle = Vector3.Angle(direction, npc.transform.forward);
        direction.y = 0f;

        npc.transform.rotation = Quaternion.Slerp(npc.transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * rotSpeed);
        if (!CanAttackPlayer())
        {
            nextState = new Idle(npc, agent, anim, player, cube);
            stage = EVENT.EXIT;
        }
    }

    public override void Exit()
    {
        anim.ResetTrigger("isShooting");
        shoot.Stop();
        base.Exit();
    }
}

public class Cube : State
{
    public Cube(GameObject _npc, NavMeshAgent _agent, Animator _anim, Transform _player, Transform _cube)
        : base(_npc, _agent, _anim, _player, _cube)
    {
        name = STATE.CUBE;
    }

    public override void Enter()
    {
        anim.SetTrigger("isRunning");
        agent.speed = 5;
        base.Enter();
    }
    public override void Update()
    {
        agent.SetDestination(cube.position);
        if (agent.remainingDistance < 1)
        {
            nextState = new Idle(npc, agent, anim, player, cube);
            stage = EVENT.EXIT;
        }
    }
    public override void Exit()
    {
        anim.ResetTrigger("isRunning");
        base.Exit();
    }
}
