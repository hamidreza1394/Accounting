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
