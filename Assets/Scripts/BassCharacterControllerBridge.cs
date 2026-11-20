using System.Collections;
using UnityEngine;

/// <summary>
/// Connects BassActionMapper to TopDown3DCharacterController.
/// 
/// BassInputHandler emits BassSignal.
/// BassActionMapper converts BassSignal.frequency into BassActionVerb.
/// This bridge sends BassActionVerb to the character controller.
/// </summary>
public class BassCharacterControllerBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private bool autoFindBassActionMapper = true;
    [SerializeField] private BassActionMapper bassActionMapper;

    [SerializeField] private bool autoFindCharacterController = true;
    [SerializeField] private TopDown3DCharacterController characterController;

    [Header("Debug")]
    [SerializeField] private bool printDebug = true;

    private bool isSubscribed;

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (enabled && !isSubscribed)
        {
            if (bassActionMapper == null && autoFindBassActionMapper)
            {
                bassActionMapper = FindFirstObjectByType<BassActionMapper>();
            }

            if (characterController == null && autoFindCharacterController)
            {
                characterController = FindFirstObjectByType<TopDown3DCharacterController>();
            }

            if (bassActionMapper != null && characterController != null)
            {
                Subscribe();
                yield break;
            }

            yield return null;
        }
    }

    private void Subscribe()
    {
        if (bassActionMapper == null || characterController == null)
        {
            return;
        }

        if (isSubscribed)
        {
            return;
        }

        bassActionMapper.BassActionMapped += OnBassActionMapped;
        isSubscribed = true;

        if (printDebug)
        {
            Debug.Log("BassCharacterControllerBridge subscribed to BassActionMapper.");
        }
    }

    private void Unsubscribe()
    {
        if (bassActionMapper != null && isSubscribed)
        {
            bassActionMapper.BassActionMapped -= OnBassActionMapped;
        }

        isSubscribed = false;
    }

    private void OnBassActionMapped(BassActionVerb actionVerb, BassSignal signal)
    {
        if (printDebug)
        {
            Debug.Log(
                $"BassCharacterControllerBridge | " +
                $"Frequency: {signal.frequency:F2} Hz → Sending Action: {actionVerb}"
            );
        }

        characterController.TriggerBassAction(actionVerb);
    }
}