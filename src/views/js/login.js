document.addEventListener("DOMContentLoaded", () => {
  const header = "STI-Authorization"
  const form = document.querySelector(`form`)
  const go = form.querySelector(`output[name="goto"]`).value

  const query = async (username, password) => {
    try {
      const encoded = `Basic ${btoa(`${username}:${password}`)}`
      const params = { method: "POST", headers: { [header]: encoded } }
      return await (await fetch("", params)).json()
    } catch (e) {
      alert(e.message)
    }
  }

  form.onsubmit = async (e) => {
    e.preventDefault()
    const { currentTarget: ct } = e
    const username = ct.querySelector(`input[name="username"]`).value
    const password = ct.querySelector(`input[name="password"]`).value
    const { ok } = await query(username, password)
    if (!ok) {
      alert(`🙅‍♀️🔐`)
    } else {
      location.href = go
    }
  }

  form.querySelector(`input[name="username"]`).focus()
})
