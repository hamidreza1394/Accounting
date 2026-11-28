import assert from "node:assert/strict";
import test from "node:test";

import { createSetupState } from "../../Vendor/Vendor.Host/Development/StaticFiles/setup-state.mjs";

test("a late successful connection test cannot restore Save after provisioning starts", () => {
  const state = createSetupState();
  const pendingTest = state.beginTest("setup-connection");
  assert.ok(pendingTest);

  assert.equal(state.beginProvisioning(), true);
  assert.equal(state.completeTest(pendingTest, "setup-connection", true), "stale");
  assert.equal(state.canSave("setup-connection", true), false);

  state.endProvisioning();
  assert.equal(state.canSave("setup-connection", true), false);
});

test("changing form values invalidates a pending test result", () => {
  const state = createSetupState();
  const pendingTest = state.beginTest("old-connection");
  assert.ok(pendingTest);

  state.invalidateTest();

  assert.equal(state.completeTest(pendingTest, "new-connection", true), "stale");
  assert.equal(state.canSave("new-connection", true), false);
});

test("only the exact successfully tested signature can be saved", () => {
  const state = createSetupState();
  const testToken = state.beginTest("connection-a");
  assert.ok(testToken);

  assert.equal(state.completeTest(testToken, "connection-a", true), "passed");
  assert.equal(state.canSave("connection-a", true), true);
  assert.equal(state.canSave("connection-b", true), false);
  assert.equal(state.canSave("connection-a", false), false);
});

test("Save and Provisioning mutations cannot overlap", () => {
  const state = createSetupState();
  const testToken = state.beginTest("connection-a");
  assert.ok(testToken);
  assert.equal(state.completeTest(testToken, "connection-a", true), "passed");

  assert.equal(state.beginSave("connection-a", true), true);
  assert.equal(state.isBusy(), true);
  assert.equal(state.beginProvisioning(), false);
  assert.equal(state.beginTest("connection-a"), null);
  assert.equal(state.canSave("connection-a", true), false);

  state.endSave();
  assert.equal(state.isBusy(), false);
  assert.equal(state.beginProvisioning(), true);
  assert.equal(state.beginSave("connection-a", true), false);
  assert.equal(state.beginTest("connection-a"), null);
  state.endProvisioning();
});

test("Delete and Provisioning credential writes cannot overlap", () => {
  const state = createSetupState();
  assert.equal(state.beginDelete(), true);
  assert.equal(state.isBusy(), true);
  assert.equal(state.beginProvisioning(), false);
  assert.equal(state.beginSave("connection-a", true), false);
  assert.equal(state.beginTest("connection-a"), null);
  state.endDelete();
  assert.equal(state.isBusy(), false);
  assert.equal(state.beginProvisioning(), true);
  assert.equal(state.beginDelete(), false);
  state.endProvisioning();
});

test("a failed or stale test never enables Save", () => {
  const state = createSetupState();
  const failedTest = state.beginTest("connection-a");
  assert.ok(failedTest);
  assert.equal(state.completeTest(failedTest, "connection-a", false), "failed");
  assert.equal(state.canSave("connection-a", true), false);

  const staleTest = state.beginTest("connection-a");
  assert.ok(staleTest);
  assert.equal(state.completeTest(staleTest, "connection-b", true), "failed");
  assert.equal(state.canSave("connection-a", true), false);
});

test("an inspection can start and holds a single inspection lock", () => {
  const state = createSetupState();
  const inspectToken = state.beginInspection();
  assert.ok(inspectToken);
  assert.equal(state.isInspectionInFlight(), true);
  assert.equal(state.isBusy(), true);

  assert.equal(state.completeInspection(inspectToken, true), "passed");
  assert.equal(state.isInspectionInFlight(), false);
  assert.equal(state.isBusy(), false);
});

test("a second inspection is rejected while the first inspection is in flight", () => {
  const state = createSetupState();
  const first = state.beginInspection();
  assert.ok(first);

  assert.equal(state.beginInspection(), null);
  assert.equal(state.isInspectionInFlight(), true);

  assert.equal(state.completeInspection(first, true), "passed");
  const second = state.beginInspection();
  assert.ok(second);
  assert.equal(state.completeInspection(second, true), "passed");
});

test("editing the form during an inspection cannot re-enable or restart Inspect", () => {
  const state = createSetupState();
  const inspectToken = state.beginInspection();
  assert.ok(inspectToken);

  // The operator edits the form while the inspect request is running:
  // invalidateTest() fires, but the inspection lock must survive it.
  state.invalidateTest();

  assert.equal(state.isInspectionInFlight(), true);
  assert.equal(state.isBusy(), true);
  assert.equal(state.beginInspection(), null);

  // The late response belongs to the old inputs and is discarded as stale;
  // the finally path (endInspection) then releases the lock.
  assert.equal(state.completeInspection(inspectToken, true), "stale");
  assert.equal(state.isInspectionInFlight(), true);
  state.endInspection();
  assert.equal(state.isInspectionInFlight(), false);

  const next = state.beginInspection();
  assert.ok(next);
  state.endInspection();
});

test("Inspection cannot start during Save, Delete or Provisioning and mutations cannot start during Inspection", () => {
  const saveState = createSetupState();
  const testToken = saveState.beginTest("connection-a");
  assert.ok(testToken);
  assert.equal(saveState.completeTest(testToken, "connection-a", true), "passed");
  assert.equal(saveState.beginSave("connection-a", true), true);
  assert.equal(saveState.beginInspection(), null);
  saveState.endSave();

  const deleteState = createSetupState();
  assert.equal(deleteState.beginDelete(), true);
  assert.equal(deleteState.beginInspection(), null);
  deleteState.endDelete();

  const provisioningState = createSetupState();
  assert.equal(provisioningState.beginProvisioning(), true);
  assert.equal(provisioningState.beginInspection(), null);
  provisioningState.endProvisioning();

  // Reverse direction: an in-flight inspection rejects every credential mutation.
  const inspectState = createSetupState();
  const inspectToken = inspectState.beginInspection();
  assert.ok(inspectToken);
  assert.equal(inspectState.beginSave("connection-a", true), false);
  assert.equal(inspectState.beginDelete(), false);
  assert.equal(inspectState.beginProvisioning(), false);
  assert.equal(inspectState.completeInspection(inspectToken, true), "passed");
});

test("after a successful inspection a new inspection can start", () => {
  const state = createSetupState();
  const first = state.beginInspection();
  assert.ok(first);
  assert.equal(state.completeInspection(first, true), "passed");
  assert.equal(state.isInspectionInFlight(), false);

  const second = state.beginInspection();
  assert.ok(second);
  assert.notEqual(second.generation, first.generation);
  assert.equal(state.completeInspection(second, true), "passed");
});

test("a failed inspection and the finally path release the inspection lock", () => {
  const failureState = createSetupState();
  const failedToken = failureState.beginInspection();
  assert.ok(failedToken);
  assert.equal(failureState.completeInspection(failedToken, false), "failed");
  assert.equal(failureState.isInspectionInFlight(), false);
  const retry = failureState.beginInspection();
  assert.ok(retry);
  failureState.endInspection();

  // Exception/transport path: the finally hook releases the lock unconditionally.
  const exceptionState = createSetupState();
  const exceptionToken = exceptionState.beginInspection();
  assert.ok(exceptionToken);
  exceptionState.endInspection();
  assert.equal(exceptionState.isInspectionInFlight(), false);

  // Inspect must be possible again after the released lock.
  const reinspect = exceptionState.beginInspection();
  assert.ok(reinspect);
  assert.equal(exceptionState.completeInspection(reinspect, true), "passed");

  // Double release is a no-op and never resurrects a phantom lock.
  exceptionState.endInspection();
  assert.equal(exceptionState.isInspectionInFlight(), false);
});

test("a stale inspection response cannot overwrite a newer inspection result", () => {
  const state = createSetupState();
  const oldToken = state.beginInspection();
  assert.ok(oldToken);

  // The operator changes inputs (invalidateTest) and starts a new inspection
  // after the old one was released by the finally path.
  state.invalidateTest();
  state.endInspection();
  const newToken = state.beginInspection();
  assert.ok(newToken);

  // The old response arriving now is stale and cannot take the lock or render.
  assert.equal(state.completeInspection(oldToken, true), "stale");
  assert.equal(state.isInspectionInFlight(), true);

  // The newer inspection still completes normally.
  assert.equal(state.completeInspection(newToken, true), "passed");
  assert.equal(state.isInspectionInFlight(), false);
});

test("Test and Inspection are mutually exclusive under the state machine contract", () => {
  const testFirstState = createSetupState();
  const testToken = testFirstState.beginTest("connection-a");
  assert.ok(testToken);
  assert.equal(testFirstState.beginInspection(), null);
  assert.equal(testFirstState.completeTest(testToken, "connection-a", true), "passed");

  const inspectFirstState = createSetupState();
  const inspectToken = inspectFirstState.beginInspection();
  assert.ok(inspectToken);
  assert.equal(inspectFirstState.beginTest("connection-a"), null);
  assert.equal(inspectFirstState.completeInspection(inspectToken, true), "passed");

  // After either probe finishes, the other may run.
  const nextTest = inspectFirstState.beginTest("connection-a");
  assert.ok(nextTest);
  assert.equal(inspectFirstState.completeTest(nextTest, "connection-a", true), "passed");
  const nextInspect = inspectFirstState.beginInspection();
  assert.ok(nextInspect);
  assert.equal(inspectFirstState.completeInspection(nextInspect, true), "passed");
});
