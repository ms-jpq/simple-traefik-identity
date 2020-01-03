const main = () => {

  const query = async () => {
    try {
      const params = {
        method: "POST",
        headers: {
          ["STI-Deauthorization"]: `👋`,
        }
      }
      return await (await fetch("/", params)).text()
    } catch (e) {
      alert(e.message)
    }
  }

  const submit = async (e) => {
    e.preventDefault()
    await query()
    location.reload()
  }

  const form = document.querySelector(`form`)
  form.onsubmit = submit

  console.log("🙆‍♀️ -- Form Ready -- 🙆‍♀️")
}

main()
