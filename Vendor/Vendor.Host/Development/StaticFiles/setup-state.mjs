export function createSetupState() {
  let generation = 0;
  let testInFlight = false;
  let provisioning = false;
  let saving = false;
  let testedSignature = "";

  return {
    invalidateTest() {
      generation += 1;
      testInFlight = false;
      testedSignature = "";
    },

    beginTest(signature) {
      if (provisioning || saving || testInFlight) return null;
      generation += 1;
      testInFlight = true;
      testedSignature = "";
      return Object.freeze({ generation, signature });
    },

    completeTest(token, currentSignature, succeeded) {
      if (!token || token.generation !== generation || !testInFlight) return "stale";
      testInFlight = false;
      if (provisioning || saving || !succeeded || token.signature !== currentSignature) return "failed";
      testedSignature = token.signature;
      return "passed";
    },

    beginSave(signature, canSave) {
      if (provisioning || saving || testInFlight || !canSave || testedSignature === "" || testedSignature !== signature) {
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

    beginProvisioning() {
      if (provisioning || saving) return false;
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
      return !provisioning && !saving && !testInFlight &&
        canSave && testedSignature !== "" && testedSignature === signature;
    },

    isBusy() {
      return provisioning || saving;
    },

    isTestInFlight() {
      return testInFlight;
    }
  };
}
