const form=document.querySelector("#connection-form");
const testButton=document.querySelector("#test-button");
const saveButton=document.querySelector("#save-button");
const result=document.querySelector("#result");
const sourceNotice=document.querySelector("#source-notice");
const savedSection=document.querySelector("#saved-section");
const deleteButton=document.querySelector("#delete-button");
const provisionSection=document.querySelector("#provision-section");
const provisionButton=document.querySelector("#provision-button");
const provisionResult=document.querySelector("#provision-result");
const provisionStages=document.querySelector("#provision-stages");
let requestToken="";
let testedSignature="";
let canSave=false;

async function api(path,options={}) {
  const response=await fetch(path,{credentials:"same-origin",cache:"no-store",...options});
  const body=await response.json().catch(()=>({}));
  return {response,body};
}
async function loadPageState() {
  const token=await api("/api/development/database-setup/antiforgery");
  requestToken=token.body.requestToken||"";
  const {body}=await api("/api/development/database-setup/status");
  canSave=body.canSave===true;
  savedSection.hidden=body.savedInWindowsCredentialManager!==true;
  provisionSection.hidden=!canSave;
  if(!canSave) {
    sourceNotice.hidden=false;
    sourceNotice.textContent=body.source==="environment"
      ?"اتصال از متغیر محیطی می‌آید؛ تغییر آن باید در تنظیمات اجرای Visual Studio انجام شود."
      :"اتصال از تنظیمات برنامه می‌آید؛ این صفحه برای جلوگیری از نادیده‌گرفته‌شدن مقدار ذخیره، ذخیره یا حذف را غیرفعال کرده است.";
    saveButton.disabled=true;
    deleteButton.disabled=true;
  }
}
function signature() {
  const data=new FormData(form);
  return JSON.stringify([...data.entries()].map(([key,value])=>[key,key==="password"||key==="provision-password"||key==="runtime-login-password"?"provided":value]));
}
function payload() {
  const data=new FormData(form);
  return {
    server:data.get("server"),database:data.get("database"),
    authentication:data.get("authentication"),username:data.get("username"),
    password:data.get("password"),trustServerCertificate:data.has("trustServerCertificate")
  };
}
function provisionPayload() {
  const connection=payload();
  const adminAuth=document.querySelector('input[name="provision-auth"]:checked').value;
  const result={
    server:connection.server,database:connection.database,
    authentication:adminAuth,
    username:document.querySelector('input[name="provision-username"]').value,
    password:document.querySelector('input[name="provision-password"]').value,
    trustServerCertificate:document.querySelector('input[name="provision-trust"]').checked,
    runtimeAuthentication:connection.authentication,
    runtimeUsername:connection.username||"",
    runtimePassword:connection.password||"",
    createRuntimeLogin:false
  };
  if(document.querySelector("#create-runtime-login").checked) {
    const loginName=document.querySelector('input[name="runtime-login-name"]').value.trim();
    const loginPassword=document.querySelector('input[name="runtime-login-password"]').value;
    if(loginName&&loginPassword) {
      result.runtimeAuthentication="sql";
      result.runtimeUsername=loginName;
      result.runtimePassword=loginPassword;
      result.createRuntimeLogin=true;
    }
  }
  return result;
}
async function postProvision(stage,body) {
  const {response,body:responseBody}=await api(`/api/development/database-setup/provision/${stage}`,{
    method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":requestToken},
    body:JSON.stringify(body)
  });
  return {response,body:responseBody};
}
function setStage(index,state,note="") {
  const item=provisionStages.querySelector(`li[data-stage="${index}"]`);
  if(!item)return;
  item.className=state;
  item.querySelector(".note").textContent=note;
}
function resetStages() {
  for(let index=1;index<=7;index++)setStage(index,"pending","");
}
function showProvisionResult(message,kind) {
  provisionResult.textContent=message||"";
  provisionResult.className="result "+(kind||"");
}
function markRemainingPending(from) {
  for(let index=from;index<=7;index++)setStage(index,"pending","");
}
document.querySelectorAll('input[name="authentication"]').forEach(input=>input.addEventListener("change",()=>{
  document.querySelector("#sql-credentials").hidden=document.querySelector('input[name="authentication"]:checked').value!=="sql";
  saveButton.disabled=true;testedSignature="";
}));
document.querySelectorAll('input[name="provision-auth"]').forEach(input=>input.addEventListener("change",()=>{
  document.querySelector("#provision-sql-credentials").hidden=document.querySelector('input[name="provision-auth"]:checked').value!=="sql";
}));
document.querySelector("#create-runtime-login").addEventListener("change",event=>{
  document.querySelector("#runtime-login-fields").hidden=!event.target.checked;
});
form.addEventListener("input",()=>{saveButton.disabled=true;testedSignature="";});
async function performTest() {
  result.className="result";result.textContent="در حال آزمون اتصال…";
  saveButton.disabled=true;testedSignature="";
  const {response,body}=await api("/api/development/database-setup/test",{
    method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":requestToken},body:JSON.stringify(payload())
  });
  result.textContent=body.message||"آزمون اتصال انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  if(response.ok){testedSignature=signature();saveButton.disabled=!canSave;}
  return response.ok;
}
testButton.addEventListener("click",async()=>{await performTest();});
form.addEventListener("submit",async event=>{
  event.preventDefault();
  if(!testedSignature||testedSignature!==signature()||!canSave)return;
  saveButton.disabled=true;
  result.className="result";result.textContent="اتصال دوباره بررسی و ذخیره می‌شود…";
  const {response,body}=await api("/api/development/database-setup/save",{
    method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":requestToken},body:JSON.stringify(payload())
  });
  result.textContent=body.message||"ذخیره انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  if(response.ok){testedSignature="";savedSection.hidden=false;setStage(7,"success","اتصال اجرا ذخیره شد؛ برنامه را دوباره اجرا کنید.");}
});
deleteButton.addEventListener("click",async()=>{
  if(!confirm("تنظیم اتصال ذخیره‌شده برای این کاربر ویندوز حذف شود؟"))return;
  const {response,body}=await api("/api/development/database-setup/saved",{
    method:"DELETE",headers:{"X-CSRF-TOKEN":requestToken}
  });
  result.textContent=body.message||"حذف انجام نشد.";
  result.className="result "+(response.ok?"success":"error");
  if(response.ok)savedSection.hidden=true;
});
function applyRuntimeAutofill(runtime) {
  if(!runtime||runtime.runtimeAuthentication!=="sql"||!runtime.runtimeUsername)return;
  const sqlRadio=document.querySelector('input[name="authentication"][value="sql"]');
  sqlRadio.checked=true;
  sqlRadio.dispatchEvent(new Event("change"));
  form.querySelector('input[name="username"]').value=runtime.runtimeUsername;
  if(runtime.runtimePassword)form.querySelector('input[name="password"]').value=runtime.runtimePassword;
  form.dispatchEvent(new Event("input"));
}
provisionButton.addEventListener("click",async()=>{
  if(!canSave)return;
  const data=provisionPayload();
  if(!data.server||!data.database){showProvisionResult("ابتدا آدرس سرور و نام دیتابیس را در فرم بالا وارد کنید.","error");return;}
  if(data.authentication==="sql"&&(!data.username||!data.password)){showProvisionResult("برای ورود مدیریتی SQL نام کاربری و گذرواژه لازم است.","error");return;}
  if(data.createRuntimeLogin&&(!data.runtimeUsername||!data.runtimePassword)){showProvisionResult("برای ساخت نام کاربری اجرا، نام و گذرواژهٔ آن لازم است.","error");return;}
  if(data.runtimeAuthentication==="sql"&&!data.createRuntimeLogin&&(!data.runtimeUsername||!data.runtimePassword)){showProvisionResult("برای آماده‌سازی دسترسی اجرای SQL، نام کاربری و گذرواژهٔ اجرا را در فرم بالا وارد کنید یا گزینهٔ ساخت نام کاربری محدود را فعال کنید.","error");return;}
  provisionButton.disabled=true;
  resetStages();
  showProvisionResult("راه‌اندازی خودکار در جریان است…","");
  try{
    setStage(1,"running");setStage(2,"running");
    const check=await postProvision("check",data);
    if(!check.response.ok){setStage(1,"failed");setStage(2,"failed");showProvisionResult(check.body.message||"بررسی اتصال مدیریتی انجام نشد.","error");return;}
    setStage(1,"success");
    if(check.body.databaseState==="foreign"){setStage(2,"warning");showProvisionResult(check.body.message,"error");return;}
    setStage(2,"success",check.body.databaseState==="missing"?"دیتابیس هدف موجود نیست":"دیتابیس هدف موجود است");
    markRemainingPending(3);
    setStage(3,"running");
    const create=await postProvision("create",data);
    if(!create.response.ok){setStage(3,"failed");showProvisionResult(create.body.message||"ایجاد دیتابیس انجام نشد.","error");return;}
    setStage(3,"success",create.body.created?"دیتابیس ایجاد شد":"دیتابیس از قبل موجود بود");
    markRemainingPending(4);
    setStage(4,"running");
    const migrate=await postProvision("migrate",data);
    if(!migrate.response.ok){setStage(4,"failed");showProvisionResult(migrate.body.message||"اجرای مهاجرت‌ها انجام نشد.","error");return;}
    setStage(4,"success",`تاریخچهٔ مهاجرت: ${migrate.body.totalApplied}/${migrate.body.expectedTotal} (اعمال این اجرا: ${migrate.body.appliedNow})`);
    markRemainingPending(5);
    setStage(5,"running");
    const runtime=await postProvision("runtime",data);
    if(!runtime.response.ok){setStage(5,"failed");showProvisionResult(runtime.body.message||"آماده‌سازی دسترسی اجرا انجام نشد.","error");return;}
    setStage(5,"success",runtime.body.loginCreated?"نام کاربری اجرای محدود ساخته شد":"دسترسی اجرا به‌روزرسانی شد");
    applyRuntimeAutofill(data);
    showProvisionResult("دیتابیس آماده است؛ اکنون اتصال اجرا آزمون و ذخیره می‌شود…","success");
    markRemainingPending(6);
    setStage(6,"running");
    const testOk=await performTest();
    if(testOk){setStage(6,"success","اتصال اجرا تأیید شد؛ اکنون ذخیره کنید.");}
    else{setStage(6,"failed","اتصال اجرا تأیید نشد؛ پیام فرم بالا را بررسی کنید.");}
  } finally {
    provisionButton.disabled=false;
  }
});
loadPageState();
