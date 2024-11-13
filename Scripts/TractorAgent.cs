using System.Collections;
using System.Collections.Generic;
using System.Net;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class TractorAgent : Agent
{
    private Rigidbody rigid;

    public Vector3 startPosition;
    public Vector3 startRotation;
    public GameObject startCheckpoint;
    private int startCheckpointIndex;

    public Vector3 reversePosition;
    public Vector3 reverseRotation;
    public GameObject reverseCheckpoint;
    private int reverseCheckpointIndex;

    // private Vector3 targetPoint;

    private int moveZ = 0;
    private int turnX = 0;
    public float moveSpeed;
    public float turnSpeed;
    private float defaultAngularDrag;

    private List<GameObject> checkpoints;
    [SerializeField] private int nextCheckpoint = 0;

    private bool isReverse = false;
    private int reverseCount = 0;

    private Vector3 checkpointForward;
    private float directionDot;

    private void Start()
    {
        rigid = GetComponent<Rigidbody>();

        checkpoints = GameManager.Instance.checkpoints.checkpoints;

        startCheckpointIndex = checkpoints.IndexOf(startCheckpoint);
        reverseCheckpointIndex = checkpoints.IndexOf(reverseCheckpoint);

        defaultAngularDrag = rigid.angularDrag;
    }

    public override void OnEpisodeBegin()
    {
        // base.OnEpisodeBegin();

        rigid.velocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;

        isReverse = UnityEngine.Random.Range(0, 2) != 0;
        if (isReverse == false) {
            rigid.position = startPosition;
            rigid.rotation = Quaternion.Euler(startRotation);
            nextCheckpoint = startCheckpointIndex;
            // targetPoint = checkpoints[reverseCheckpointIndex].transform.position;
        }
        else {
            rigid.position = reversePosition;
            rigid.rotation = Quaternion.Euler(reverseRotation);
            nextCheckpoint = reverseCheckpointIndex;
            // targetPoint = checkpoints[startCheckpointIndex].transform.position;
        }

        if (GameManager.Instance.mainCamera.isTraining) {
            reverseCount = 0;
        }
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        // base.CollectObservations(sensor);

        // üũ����Ʈ ���� ��ġ��
        checkpointForward = checkpoints[nextCheckpoint].transform.forward;
        if (isReverse == false) {
            directionDot = Vector3.Dot(checkpointForward, transform.forward);
        }
        else {
            directionDot = Vector3.Dot(-checkpointForward, transform.forward);
        }
        sensor.AddObservation(directionDot);

        // ���� ��ġ�� ��ǥ ����
        // sensor.AddObservation(rigid.position);
        // sensor.AddObservation(targetPoint);

        // Reverse ���� ���� �� �ٽ� �ݴ� �������� ȸ���ϱ� ���� ����ġ
        sensor.AddObservation(isReverse);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // base.OnActionReceived(actions);

        moveZ = actions.DiscreteActions[0]; // 0: ����, 1: ��, 2: ��
        turnX = actions.DiscreteActions[1]; // 0: ����, 1: ����, 2: ��


        // �ð��� ���Ƽ
        AddReward(-0.001f); // AddReward(-1f / 1000);

        // �̵� ������ üũ����Ʈ ����� ��ġ�� �� ���� �ο�
        if (directionDot < 0 || directionDot > 0.9f) {
            AddReward(directionDot * (moveZ == 1 ? 1 : 0) * 0.01f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // base.Heuristic(actionsOut);

        ActionSegment<int> discreteSegment = actionsOut.DiscreteActions;

        float moveZ = Input.GetAxisRaw("Vertical");
        float turnX = Input.GetAxisRaw("Horizontal");
        moveZ = moveZ == -1 ? 2 : moveZ;
        turnX = turnX == -1 ? 2 : turnX;

        discreteSegment[0] = (int)moveZ;
        discreteSegment[1] = (int)turnX;
    }

    private void FixedUpdate()
    {
        Vector3 moveDirection = transform.forward * (moveZ == 2 ? -1 : moveZ) * moveSpeed;
        rigid.velocity = moveDirection;

        float turnDirection = (turnX == 2 ? -1 : turnX) * turnSpeed;
        if (turnDirection != 0 && rigid.angularDrag == defaultAngularDrag) {
            rigid.angularDrag = 0.05f;
        }
        else if (turnDirection == 0 && rigid.angularDrag != defaultAngularDrag) {
            rigid.angularDrag = defaultAngularDrag;
        }
        rigid.angularVelocity = new Vector3(0f, turnDirection, 0f);

        // ���� ���� ���� ���
        if (Input.GetKeyDown(KeyCode.Space)) {
            Debug.Log($"Current Reward: {GetCumulativeReward()} // {nextCheckpoint}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("Checkpoint")) return;

        int checkpointIndex = checkpoints.IndexOf(other.gameObject);
        if (checkpointIndex == nextCheckpoint) {
            AddReward(2f); // �ùٸ� üũ����Ʈ ���� �� ����
            // Debug.Log($"1 + {isReverse} + {GetCumulativeReward()} + {nextCheckpoint}");
            // Debug.Log("OK");

            // ���� üũ����Ʈ ���� ��
            if ((isReverse && checkpointIndex == startCheckpointIndex) || (!isReverse && checkpointIndex == reverseCheckpointIndex)) {
                AddReward(20f); // ���� ����

                // ���� �������� ���� (���� �ݴ� ��ȯ)
                isReverse = !isReverse;
                nextCheckpoint = isReverse ? reverseCheckpointIndex - 1 : startCheckpointIndex + 1;
                // targetPoint = isReverse ? checkpoints[startCheckpointIndex].transform.position : checkpoints[reverseCheckpointIndex].transform.position;

                // �ݺ� Ƚ���� �ʰ��Ǹ� ���Ǽҵ� ����
                reverseCount++;
                if (reverseCount >= 3 && GameManager.Instance.mainCamera.isTraining) {
                    EndEpisode();
                    return;
                }

                // Debug.Log($"{isReverse} + {GetCumulativeReward()} + {nextCheckpoint}");
            }
            else {
                // ���� üũ����Ʈ�� ������Ʈ
                nextCheckpoint = isReverse ? nextCheckpoint - 1 : nextCheckpoint + 1;
            }
        }
        else {
            // �߸��� üũ����Ʈ�� �����ϸ� ���Ƽ �ο�
            AddReward(-1f);
            // Debug.Log($"2 + {isReverse} + {GetCumulativeReward()} + {nextCheckpoint}");
            // Debug.Log("NO");

            // �ʹ� �� üũ����Ʈ ���� �� ���� ���Ƽ
            if (checkpointIndex <= startCheckpointIndex - 5 || checkpointIndex >= reverseCheckpointIndex + 5) {
                AddReward(-4f);
                EndEpisode();
                // Debug.Log($"{isReverse} + {GetCumulativeReward()} + {nextCheckpoint}");

                // �ſ� �� üũ����Ʈ ���� �� ���Ǽҵ� ����
                //if (checkpointIndex <= startCheckpointIndex - 10 || checkpointIndex >= reverseCheckpointIndex + 10) {
                //    AddReward(-20f);
                //    EndEpisode();
                //}
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall")) {
            AddReward(-1f);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall")) {
            AddReward(-0.1f);

            if (GetCumulativeReward() < -40f) {
                AddReward(-1f);
                if (GameManager.Instance.mainCamera.isTraining) {
                    EndEpisode();
                }
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall")) {
            AddReward(0.5f);
        }
    }

    // Animal == Obstacle
}
