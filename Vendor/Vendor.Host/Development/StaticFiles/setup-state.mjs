export function createSetupState() {
  let generation = 0;
  let testInFlight = false;
  let testFlightGeneration = 0;
  let inspectionInFlight = false;
  let provisioning = false;
  let saving = false;
  let testedSignature = "";

  return {
    // Editing the form invalidates the pending approval (generation) but must
    // never release an in-flight request's lock: until the response settles,
    // no new test or inspection may start.
    invalidateTest() {
      generation += 1;
      testedSignature = "";
    },

    beginTest(signature) {
      if (provisioning || saving || testInFlight || inspectionInFlight) return null;
      generation += 1;
      testInFlight = true;
      testFlightGeneration = generation;
      testedSignature = "";
      return Object.freeze({ generation, signature });
    },

    completeTest(token, currentSignature, succeeded) {
      // Only the token of the currently in-flight request may release the lock;
      // a response from a superseded request must never free a newer test's lock.
      if (!token || token.generation !== testFlightGeneration || !testInFlight) return "stale";
      const resultIsStale = token.generation !== generation;
      testInFlight = false;
      // The request has settled, but a result captured before form edits no
      // longer belongs to the current inputs and is discarded, not rendered.
      if (resultIsStale) return "stale";
      if (provisioning || saving || !succeeded || token.signature !== currentSignature) return "failed";
      testedSignature = token.signature;
      return "passed";
    },

    beginInspection() {
      if (provisioning || saving || testInFlight || inspectionInFlight) return null;
      generation += 1;
      inspectionInFlight = true;
      return Object.freeze({ generation });
    },

    completeInspection(token, succeeded) {
      if (!token || token.generation !== generation || !inspectionInFlight) return "stale";
      inspectionInFlight = false;
      if (provisioning || saving || !succeeded) return "failed";
      return "passed";
    },

    endInspection() {
      inspectionInFlight = false;
    },

    beginSave(signature, canSave) {
      if (provisioning || saving || testInFlight || inspectionInFlight || !canSave || testedSignature === "" || testedSignature !== signature) {
        return false;
      }

      saving = true;
      generation += 1;
      testedSignature = "";
      return true;
    },

    endSave() {
      saving = false;
    },

    beginDelete() {
      if (provisioning || saving || inspectionInFlight) return false;
      saving = true;
      generation += 1;
      testInFlight = false;
      testedSignature = "";
      return true;
    },

    endDelete() {
      saving = false;
    },

    beginProvisioning() {
      if (provisioning || saving || inspectionInFlight) return false;
      provisioning = true;
      generation += 1;
      testInFlight = false;
      testedSignature = "";
      return true;
    },

    endProvisioning() {
      provisioning = false;
    },

    canSave(signature, canSave) {
      return !provisioning && !saving && !testInFlight && !inspectionInFlight &&
        canSave && testedSignature !== "" && testedSignature === signature;
    },

    isBusy() {
      return provisioning || saving || inspectionInFlight;
    },

    isTestInFlight() {
      return testInFlight;
    },

    isInspectionInFlight() {
      return inspectionInFlight;
    }
  };
}
