import { createSetupState } from "./database-setup-state.mjs";

const form=document.querySelector("#connection-form");
const testButton=document.querySelector("#test-button");
const inspectButton=document.querySelector("#inspect-button");
const provisionButton=document.querySelector("#provision-button");
const saveButton=document.querySelector("#save-button");
const result=document.querySelector("#result");
const sourceNotice=document.querySelector("#source-notice");
const savedSection=document.querySelector("#saved-section");
const deleteButton=document.querySelector("#delete-button");
const challengeInput=document.querySelector("#challenge-input");
const stagesPanel=document.querySelector("#stages-panel");
const stageList=document.querySelector("#stage-list");
const inspectionPanel=document.querySelector("#inspection-panel");
const runtimePanel=document.querySelector("#runtime-panel");
let requestToken="";
const setupState=createSetupState();
let canSave=false;

const stageNames={connection:"اعتبارسنجی اتصال",test:"آزمون اتصال",inspect:"بازرسی سرور و دیتابیس",createDatabase:"ایجاد دیتابیس",migrate:"اعمال Migrationها",runtimeIdentity:"هویت Runtime کم‌اختیار",verify:"بررسی آمادگی"};
const stateNames={pending:"در انتظار",running:"در حال اجرا",success:"انجام شد",skipped:"رد شد",failed:"ناموفق"};

async function api(path,options={}) {
  const response=await fetch(path,{credentials:"same-origin",cache:"no-store",...options});
  const body=await response.json().catch(()=>({}));
  return {response,body};
}
function challenge(){return challengeInput.value.trim();}
function guardHeaders(challengeValue=challenge()){
  return {"Content-Type":"application/json","X-CSRF-TOKEN":requestToken,"X-Faranou-Setup-Challenge":challengeValue};
}
function showResult(text,ok){result.className="result "+(ok?"success":"error");result.textContent=text;}
function requireChallenge(){
  if(challenge())return true;
  showResult("کد نشست راه‌اندازی را از کنسول فرآیند وارد کنید؛ همهٔ عملیات تغییردهنده بدون آن رد می‌شوند.",false);
  return false;
}
function guardRefusal(body){
  showResult("عملیات رد شد: کد نشست راه‌اندازی معتبر نیست یا گاردهای امنیتی (loopback/Origin/antiforgery) کامل نشد. کد را از کنسول فرآیند بررسی کنید.",false);
}
function renderStages(stages){
  stagesPanel.hidden=false;
  stageList.replaceChildren(...(stages||[]).map(stage=>{
    const item=document.createElement("li");
    item.className="stage "+stage.state;
    const title=document.createElement("div");title.className="stage-title";
    const name=document.createElement("span");name.textContent=stageNames[stage.key]||stage.key;
    const state=document.createElement("span");state.className="state";state.textContent=stateNames[stage.state]||stage.state;
    title.append(name,state);
    item.append(title);
    if(stage.message&&(stage.state==="failed"||stage.state==="running"||stage.state==="skipped")){
      const message=document.createElement("p");message.textContent=stage.message;item.append(message);
    }
    return item;
  }));
}
function renderInspection(body){
  inspectionPanel.hidden=false;
  const rows=[
    ["نسخهٔ SQL Server",body.serverVersion],
    ["دیتابیس مقصد",body.databaseExists?"موجود است":"ساخته نشده است"],
    ["نشانهٔ مالکیت Vendor",body.ownershipMarkerValid?"معتبر":"ناموجود/نامعتبر"],
    ["تاریخچهٔ Migration",body.historyTableExists?"موجود است":"ناموجود"],
    ["Migrationهای اعمال‌شده",body.appliedMigrationsCount],
    ["Migrationهای باقی‌مانده",body.pendingMigrationsCount],
    ["Login Runtime",body.runtimeLoginExists?"موجود است":"ساخته نشده است"],
    ["جدول‌های کاربر",body.userTableCount]
  ];
  const list=document.createElement("dl");
  rows.forEach(([label,value])=>{
    const term=document.createElement("dt");term.textContent=label;
    const detail=document.createElement("dd");detail.textContent=String(value);
    list.append(term,detail);
  });
  const title=document.createElement("h2");title.textContent="نتیجهٔ بازرسی";
  const summary=document.createElement("p");summary.textContent=body.message||"";
  inspectionPanel.replaceChildren(title,list,summary);
}
async function loadPageState() {
  const token=await api("/api/development/database-setup/antiforgery");
  requestToken=token.body.requestToken||"";
  const {body}=await api("/api/development/database-setup/status");
  canSave=body.canSave===true;
  savedSection.hidden=body.savedInWindowsCredentialManager!==true;
  if(!canSave) {
    sourceNotice.hidden=false;
    sourceNotice.textContent=body.source==="environment"
      ?"اتصال از متغیر محیطی می‌آید؛ تغییر آن باید در تنظیمات اجرای Visual Studio انجام شود و ذخیرهٔ اتصال Runtime انجام نمی‌شود."
      :"اتصال از تنظیمات برنامه می‌آید؛ این صفحه برای جلوگیری از نادیده‌گرفته‌شدن مقدار ذخیره، ذخیره یا حذف را غیرفعال کرده است.";
  }
  syncActionButtons();
}
function signature() {
  const data=new FormData(form);
  return JSON.stringify([...data.entries()].map(([key,value])=>[key,key==="password"||key==="runtimePassword"?"provided":value]));
}
function syncActionButtons() {
  const busy=setupState.isBusy();
  testButton.disabled=busy||setupState.isTestInFlight();
  inspectButton.disabled=busy||setupState.isInspectionInFlight();
  provisionButton.disabled=busy;
  saveButton.disabled=busy||!setupState.canSave(signature(),canSave);
  deleteButton.disabled=busy||!canSave;
}
function setFormBusy(busy) {
  form.querySelectorAll("input,select,textarea").forEach(control=>{control.disabled=busy;});
  syncActionButtons();
}
function provisioningPayload() {
  const data=new FormData(form);
  return {
    server:data.get("server"),database:data.get("database"),
    authentication:data.get("authentication"),username:data.get("username"),
    password:data.get("password"),trustServerCertificate:data.has("trustServerCertificate"),
    runtimeUsername:data.get("runtimeUsername")||null,runtimePassword:data.get("runtimePassword")||null
  };
}
document.querySelectorAll('input[name="authentication"]').forEach(input=>input.addEventListener("change",()=>{
  document.querySelector("#sql-credentials").hidden=document.querySelector('input[name="authentication"]:checked').value!=="sql";
  setupState.invalidateTest();
  syncActionButtons();
}));
form.addEventListener("input",()=>{
  setupState.invalidateTest();
  syncActionButtons();
});
testButton.addEventListener("click",async()=>{
  if(!requireChallenge()||setupState.isBusy())return;
  const testSignature=signature();
  const payload=provisioningPayload();
  const token=setupState.beginTest(testSignature);
  if(!token)return;
  syncActionButtons();
  result.className="result";result.textContent="در حال آزمون اتصال…";
  try {
    const {response,body}=await api("/api/development/database-setup/test",{
      method:"POST",headers:guardHeaders(),body:JSON.stringify(payload)
    });
    const outcome=setupState.completeTest(token,signature(),response.ok&&body.success===true);
    if(outcome==="stale")return;
    if(response.status===403){guardRefusal(body);return;}
    result.textContent=body.message||"آزمون اتصال انجام نشد.";
    result.className="result "+(outcome==="passed"?"success":"error");
  } catch {
    const outcome=setupState.completeTest(token,signature(),false);
    if(outcome!=="stale")showResult("آزمون اتصال ممکن نشد؛ اتصال صفحه به برنامه بررسی شود و دوباره تلاش کنید.",false);
  } finally {
    syncActionButtons();
  }
});
form.addEventListener("submit",async event=>{
  event.preventDefault();
  if(!requireChallenge())return;
  const payload=provisioningPayload();
  const challengeValue=challenge();
  if(!setupState.beginProvisioning())return;
  setFormBusy(true);
  result.className="result";result.textContent="راه‌اندازی هفت‌مرحله‌ای در حال اجرا است؛ این عملیات دیتابیس و هویت Runtime را می‌سازد…";
  stagesPanel.hidden=false;stageList.replaceChildren();
  runtimePanel.hidden=true;
  try {
    const token=await api("/api/development/database-setup/provision-token");
    if(!token.response.ok){showResult("دریافت نشست یک‌بارمصرف راه‌اندازی ممکن نشد؛ دوباره تلاش کنید.",false);return;}
    const {response,body}=await api("/api/development/database-setup/provision",{
      method:"POST",
      headers:{...guardHeaders(challengeValue),"X-Faranou-Setup-Token":token.body.token||""},
      body:JSON.stringify(payload)
    });
    if(response.status===403){guardRefusal(body);renderStages([]);return;}
    renderStages(body.stages);
    const ok=response.ok&&body.success===true;
    result.textContent=body.message||(ok?"راه‌اندازی کامل شد.":"راه‌اندازی ناموفق بود؛ مراحل بالا را بررسی کنید.");
    result.className="result "+(ok?"success":"error");
    if(ok&&body.runtimeUsername){
      runtimePanel.hidden=false;
      document.querySelector("#runtime-username").textContent="نام کاربری Runtime: "+body.runtimeUsername;
      const passwordWrap=document.querySelector("#runtime-password-wrap");
      passwordWrap.hidden=!body.generatedPassword;
      if(body.generatedPassword)document.querySelector("#runtime-password").textContent=body.generatedPassword;
      document.querySelector("#runtime-saved").textContent=body.savedToCredentialManager===true
        ?"اتصال Runtime برای کاربر فعلی در Windows Credential Manager ذخیره شد؛ برای اعمال، Vendor را دوباره اجرا کنید."
        :"اتصال Runtime ذخیره نشد؛ در حالت پیکربندی محیطی، اتصال Runtime باید در همان منبع تنظیم شود.";
    }
  } catch {
    // Transport failure: the outcome is genuinely unknown — some stages may have completed.
    showResult("نتیجهٔ راه‌اندازی نامشخص است؛ احتمالاً برخی مراحل روی سرور کامل شده‌اند. قبل از هر تلاش دوباره، با «بازرسی سرور و دیتابیس» وضعیت را بررسی کنید.",false);
  } finally {
    setupState.endProvisioning();
    setFormBusy(false);
  }
});
inspectButton.addEventListener("click",async()=>{
  if(!requireChallenge()||setupState.isBusy())return;
  const inspectToken=setupState.beginInspection();
  if(!inspectToken)return;
  const payload=provisioningPayload();
  syncActionButtons();
  result.className="result";result.textContent="در حال بازرسی سرور و دیتابیس…";
  inspectionPanel.hidden=true;runtimePanel.hidden=true;
  try {
    const {response,body}=await api("/api/development/database-setup/inspect",{
      method:"POST",headers:guardHeaders(),body:JSON.stringify(payload)
    });
    const outcome=setupState.completeInspection(inspectToken,response.ok&&body.success===true);
    // A response for inputs the operator has already changed must not overwrite
    // the panel or a newer inspection's state.
    if(outcome==="stale")return;
    if(response.status===403){guardRefusal(body);return;}
    renderStages(body.stages);
    if(outcome==="passed"){
      renderInspection(body);
      result.textContent=body.message||"بازرسی کامل شد.";
      result.className="result success";
    } else {
      // A failed inspection must not present its stage values as database facts.
      result.textContent=body.message||"بازرسی ناموفق بود؛ اتصال و دسترسی هویت پیکربندی بررسی شود.";
      result.className="result error";
    }
  } catch {
    const outcome=setupState.completeInspection(inspectToken,false);
    if(outcome!=="stale")showResult("بازرسی ممکن نشد؛ اتصال صفحه به برنامه بررسی شود و دوباره تلاش کنید.",false);
  } finally {
    // Transport errors, exceptions and stale responses must never leave the
    // inspection lock engaged; release it here so Inspect can run again.
    setupState.endInspection();
    syncActionButtons();
  }
});
saveButton.addEventListener("click",async()=>{
  if(!requireChallenge())return;
  const saveSignature=signature();
  if(!setupState.beginSave(saveSignature,canSave))return;
  const payload=provisioningPayload();
  setFormBusy(true);
  result.className="result";result.textContent="اتصال دوباره بررسی و ذخیره می‌شود…";
  try {
    const {response,body}=await api("/api/development/database-setup/save",{
      method:"POST",headers:guardHeaders(),body:JSON.stringify(payload)
    });
    if(response.status===403){guardRefusal(body);return;}
    result.textContent=body.message||"ذخیره انجام نشد.";
    result.className="result "+(response.ok?"success":"error");
    if(response.ok)savedSection.hidden=false;
  } catch {
    showResult("ذخیرهٔ اتصال ممکن نشد؛ وضعیت Credential Manager را دوباره بررسی کنید.",false);
  } finally {
    setupState.endSave();
    setFormBusy(false);
  }
});
deleteButton.addEventListener("click",async()=>{
  if(!requireChallenge()||!setupState.beginDelete())return;
  setFormBusy(true);
  if(!confirm("تنظیم اتصال ذخیره‌شده برای این کاربر ویندوز حذف شود؟")){
    setupState.endDelete();
    setFormBusy(false);
    return;
  }
  try {
    const {response,body}=await api("/api/development/database-setup/saved",{
      method:"DELETE",headers:{"X-CSRF-TOKEN":requestToken,"X-Faranou-Setup-Challenge":challenge()}
    });
    if(response.status===403){guardRefusal(body);return;}
    result.textContent=body.message||"حذف انجام نشد.";
    result.className="result "+(response.ok?"success":"error");
    if(response.ok)savedSection.hidden=true;
  } catch {
    showResult("حذف اتصال ذخیره‌شده ممکن نشد؛ وضعیت Credential Manager را دوباره بررسی کنید.",false);
  } finally {
    setupState.endDelete();
    setFormBusy(false);
  }
});
loadPageState();
